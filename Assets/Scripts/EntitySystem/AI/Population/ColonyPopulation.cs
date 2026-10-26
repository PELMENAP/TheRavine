using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Mathematics;

using TheRavine.EntityControl.Virology;

public sealed class ColonyPopulation
{
    private readonly GameObject   _prefab;
    private readonly Transform    _parent;
    private readonly EntityTuning _tuning;
    private readonly int          _maxPopulation;
    private readonly float        _spawnRadius;

    private readonly List<EntityModel> _entities;
    private readonly ColonyRegistry    _colonies;
    private readonly ColonyState       _wild;
    private readonly ColonyFood        _food;
    private readonly EvolutionService  _evolution;
    private readonly EntitySpatialGrid _grid;

    private readonly MotionSystem         _motion;
    private readonly InstinctSystem       _instincts;
    private readonly UtilityPriorSystem   _utility;
    private readonly CasteThresholdSystem _thresholds;
    private readonly BoidSystem           _boids;

    private readonly Queue<EntityModel> _pendingDeath = new();
    private EntityModel[] _deferredDispose = Array.Empty<EntityModel>();
    private int _deferredCount;
    private int _nextEntityId;
    private double _nextPredatorTime;

    private readonly EntityModel[] _witnesses = new EntityModel[InfectionService.MaxNeighbors];

    public event Action<EntityModel> Spawned;
    public event Action<EntityModel> Died;

    public int CivilCount => _colonies.Count - (_wild != null ? 1 : 0);

    public ColonyPopulation(GameObject prefab, Transform parent, EntityTuning tuning, int maxPopulation, float spawnRadius,
        List<EntityModel> entities, ColonyRegistry colonies, ColonyState wild, ColonyFood food, EvolutionService evolution,
        EntitySpatialGrid grid, MotionSystem motion, InstinctSystem instincts, UtilityPriorSystem utility,
        CasteThresholdSystem thresholds, BoidSystem boids)
    {
        _prefab        = prefab;
        _parent        = parent;
        _tuning        = tuning;
        _maxPopulation = maxPopulation;
        _spawnRadius   = spawnRadius;
        _entities      = entities;
        _colonies      = colonies;
        _wild          = wild;
        _food          = food;
        _evolution     = evolution;
        _grid          = grid;
        _motion        = motion;
        _instincts     = instincts;
        _utility       = utility;
        _thresholds    = thresholds;
        _boids         = boids;
    }

    public void StartPredatorClock(double now) => _nextPredatorTime = now;

    public EntityModel SpawnEntity(Vector3 position, ColonyState colony, EntityBrainContext inheritedCtx = null, EntityModel parent = null)
    {
        if (_entities.Count >= _maxPopulation || colony == null)
        {
            inheritedCtx?.Dispose();
            return null;
        }

        var go  = UnityEngine.Object.Instantiate(_prefab, position, Quaternion.identity, _parent);
        var ctx = inheritedCtx ?? colony.Brain.CreateContext();

        foreach (var expressible in go.GetComponents<IGeneticPhenotype>())
            expressible.ApplyGeneticPhenotype(ctx.CoordMLP.Params);

        var netObj = go.GetComponent<NetworkObject>();
        netObj?.Spawn();

        var viewModel = go.GetComponent<EntityViewModel>();
        var view      = go.GetComponent<EntityView>();

        viewModel.BindMotion(_motion);

        var model = new EntityModel();
        model.AssignId(++_nextEntityId);

        model.Configure(colony, ctx, viewModel, viewModel, go, _tuning, parent);
        model.Init();
        viewModel.Initialize(model);
        view.Initialize(viewModel);
        model.VisualCulling = new VisualCullingComponent(go, view.LabelObject);
        model.SetUp();

        model.Mortality.Died += () => HandleEntityDied(model);
        model.OnReproduceRequest += SpawnChild;

        model.ManagerIndex = _entities.Count;
        _entities.Add(model);
        colony.AddMember(model);
        colony.Stats.RecordBirth();
        _instincts.Reset(model.ManagerIndex, InstinctGenes.From(in ctx.CoordMLP.Params));
        _utility.Write(model.ManagerIndex, default);
        _thresholds.Reset(model.ManagerIndex, CasteThresholdSystem.Initial());
        Spawned?.Invoke(model);
        return model;
    }

    public void SpawnChild(EntityModel parent)
    {
        if (_entities.Count >= _maxPopulation || parent.IsWild) return;

        var parentParams = parent.Brain.Context.CoordMLP.Params;
        var mate         = _evolution.SelectMate(parent);

        var parentVirology = parent.Virology;
        if (mate != null && parentVirology != null
            && parentVirology.Modifiers.CytoIncompat >= SimulationRules.Active.CytoIncompatThreshold
            && mate.LineageId != parent.LineageId)
            return;

        GeneticParameters childParams;
        if (mate != null)
        {
            var mateParams = mate.Brain.Context.CoordMLP.Params;
            GeneticParameters.Crossover(in parentParams, in mateParams, out childParams);
        }
        else
        {
            childParams = parentParams.GetMutatedGeneticParameters();
        }

        var colony   = parent.Colony;
        if (colony == null) return;
        var childCtx = colony.Brain.CreateContext(childParams);
        var rules    = SimulationRules.Active;
        var pos      = parent.Motor.Position()
                     + (Vector3)RavineRandom.GetInsideCircle().normalized * rules.ChildSpawnOffset
                     + Vector3.up * rules.ChildSpawnLift;

        SpawnEntity(pos, colony, childCtx, parent);
    }

    public Vector3 RandomPosition(ColonyState colony)
    {
        var v    = RavineRandom.GetInsideCircle(_spawnRadius);
        var home = colony.Nest.Position;
        return new Vector3(home.x + v.x, _parent.position.y, home.y + v.y);
    }

    private void HandleEntityDied(EntityModel model)
    {
        if (model == null || model.IsDisposed || model.IsDeathPending) return;
        model.DeathPosition = model.Position2D;
        model.MarkDeathPending();
        _pendingDeath.Enqueue(model);
    }

    public void ProcessPendingDeaths()
    {
        while (_pendingDeath.Count > 0)
        {
            var model = _pendingDeath.Dequeue();
            if (model == null || model.IsDisposed) continue;

            model.OnReproduceRequest -= SpawnChild;
            model.CaptureFinalFitness();
            var cause = model.Mortality != null ? model.Mortality.Cause : DeathCause.Age;
            model.Colony?.Stats.RecordDeath(model.TimeAlive, cause);
            HandleLeaderDeath(model);
            BroadcastDeath(model, cause);
            _food.DropCorpse(model, cause);
            model.Brain?.CompleteTerminal(_evolution.TerminalPenaltyFor(model, cause), SimulationClock.Time);
            RemoveEntitySwapBack(model);
            Died?.Invoke(model);

            if (_deferredCount == _deferredDispose.Length)
                Array.Resize(ref _deferredDispose, Math.Max(_deferredDispose.Length << 1, 8));
            _deferredDispose[_deferredCount++] = model;
        }
    }

    private void BroadcastDeath(EntityModel model, DeathCause cause)
    {
        var colony = model.Colony;
        if (colony == null) return;

        var r    = SimulationRules.Active;
        var nest = colony.Nest;
        float2 at = model.DeathPosition;

        nest.Mark(ColonyChannel.KinDeath, at, r.KinDeathMark);
        float poiDanger = cause == DeathCause.Killed || cause == DeathCause.Virus || cause == DeathCause.Toxic
            ? r.ColonyPoiDeathDanger : 0f;
        if (poiDanger > 0f) colony.Pois.Offer(at, 0f, poiDanger);

        if (cause == DeathCause.Killed || cause == DeathCause.Virus)
        {
            nest.Mark(ColonyChannel.Danger, at, r.KinDeathDangerMark);
            nest.RaiseAlarm(r.KinDeathAlarm);
        }
        if (cause == DeathCause.Starved) nest.RaiseHunger(r.StarvedHungerRise);
        if (cause == DeathCause.Virus || cause == DeathCause.Toxic) nest.Mark(ColonyChannel.Danger, at, r.CorpseDangerMark);

        ulong lineage = model.LineageId;
        int found = _grid.FindInRadius(new Vector3(at.x, 0f, at.y), model, r.KinDeathRadius, _witnesses);
        for (int i = 0; i < found; i++)
        {
            var w = _witnesses[i];
            _witnesses[i] = null;
            if (w == null || w.IsDisposed || w.IsDeathPending) continue;

            float kin = 0f;
            if (w.Colony == colony) kin += r.KinColonyWeight;
            if (lineage != 0UL && w.LineageId == lineage) kin += r.KinLineageWeight;
            kin = math.saturate(kin);
            if (kin <= 0f) continue;

            w.AddStress(r.KinDeathStress * kin);
            w.AddExtrinsicReward(r.WitnessPenalty * kin);
        }
    }

    public void FlushDeferredDisposals()
    {
        for (int i = 0; i < _deferredCount; i++)
        {
            _deferredDispose[i].Dispose();
            _deferredDispose[i] = null;
        }
        _deferredCount = 0;
    }

    private void RemoveEntitySwapBack(EntityModel model)
    {
        int idx = model.ManagerIndex;

        if ((uint)idx >= (uint)_entities.Count || !ReferenceEquals(_entities[idx], model))
        {
            idx = _entities.IndexOf(model);
            if (idx < 0) { model.ManagerIndex = -1; return; }
        }

        model.Colony?.RemoveMember(model, _entities);

        int last = _entities.Count - 1;
        if (idx != last)
        {
            var moved = _entities[last];
            _entities[idx]    = moved;
            moved.ManagerIndex = idx;
            moved.Colony?.RemapMember(moved.ColonySlot, idx);
        }

        _instincts.RemoveSwapBack(idx, last);
        _boids.RemoveSwapBack(idx, last);
        _utility.RemoveSwapBack(idx, last);
        _thresholds.RemoveSwapBack(idx, last);
        _entities.RemoveAt(last);
        model.ManagerIndex = -1;
    }

    public EntityModel ElectLeader(ColonyState colony)
    {
        var members = colony.Members;
        EntityModel best = null;
        float bestRating = float.MinValue;
        for (int i = 0; i < members.Length; i++)
        {
            var e = _entities[members[i]];
            if (e.IsDisposed || e.IsDeathPending || e.IsJuvenile) continue;
            float rating = e.GetFitness() * e.TimeAlive;
            if (rating <= bestRating) continue;
            bestRating = rating;
            best = e;
        }
        return best;
    }

    private void HandleLeaderDeath(EntityModel model)
    {
        var colony = model.Colony;
        if (colony == null || !ReferenceEquals(colony.Leader, model)) return;

        colony.SetLeader(null);
        colony.NextElection = 0d;

        float stress = SimulationRules.Active.LeaderSuccessionStress;
        var members = colony.Members;
        for (int i = 0; i < members.Length; i++)
        {
            var e = _entities[members[i]];
            if (!ReferenceEquals(e, model) && !e.IsDisposed) e.AddStress(stress);
        }

        colony.SetLeader(ElectLeader(colony));
    }

    public void CheckExtinctions()
    {
        var r = SimulationRules.Active;
        double now = SimulationClock.TimeD;
        for (int c = 0; c < _colonies.Count; c++)
        {
            var colony = _colonies[c];
            if (colony.IsWild) continue;
            if (colony.MemberCount > 0)
            {
                colony.ExtinctSince = double.NaN;
                continue;
            }
            if (double.IsNaN(colony.ExtinctSince)) { colony.ExtinctSince = now; continue; }
            if (now - colony.ExtinctSince < r.ColonyRespawnDelay) continue;
            ReseedColony(colony, BestDonor(colony));
        }
    }

    private ColonyState BestDonor(ColonyState exclude)
    {
        ColonyState best = null;
        float bestScore = float.MinValue;
        for (int c = 0; c < _colonies.Count; c++)
        {
            var colony = _colonies[c];
            if (colony == exclude || colony.IsWild || colony.MemberCount == 0) continue;
            float score = colony.Score;
            if (score <= bestScore) continue;
            bestScore = score;
            best = colony;
        }
        return best;
    }

    private void ReseedColony(ColonyState colony, ColonyState donor)
    {
        colony.ExtinctSince = double.NaN;
        colony.EmaPrimed    = false;
        colony.Stats.Reseeds++;

        if (donor != null)
        {
            var old = colony.Brain;
            colony.ReplaceBrain(new SharedHierarchicalBrain(donor.Brain));
            old?.Dispose();
        }

        int count = SimulationRules.Active.ColonyRespawnCount;
        var donorMembers = donor != null ? donor.Members : default;
        for (int i = 0; i < count && _entities.Count < _maxPopulation; i++)
        {
            GeneticParameters genes = donorMembers.Length > 0
                ? _entities[donorMembers[RavineRandom.RangeInt(0, donorMembers.Length)]].Brain.Context.CoordMLP.Params.GetMutatedGeneticParameters()
                : GeneticParameters.Default;
            SpawnEntity(RandomPosition(colony), colony, colony.Brain.CreateContext(genes));
            if (donor != null) donorMembers = donor.Members;
        }
    }

    public void TickPredators()
    {
        var wild = _wild;
        if (wild == null) return;
        var r = SimulationRules.Active;
        double now = SimulationClock.TimeD;
        if (now < _nextPredatorTime || wild.MemberCount >= r.PredatorCount || CivilCount == 0) return;
        _nextPredatorTime = now + r.PredatorRespawnInterval;

        var home = _colonies[RavineRandom.RangeInt(0, CivilCount)].Nest.Position;
        float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
        math.sincos(a, out float sa, out float ca);
        var p = home + new float2(ca, sa) * r.PredatorSpawnDistance;
        SpawnEntity(new Vector3(p.x, _parent.position.y, p.y), wild);
    }
}
