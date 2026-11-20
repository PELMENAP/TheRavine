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
    private readonly int          _capacity;
    private readonly float        _spawnRadius;
    private readonly bool         _spawnNetworkObjects;

    private readonly List<EntityModel> _entities;
    private readonly ColonyRegistry    _colonies;
    private readonly ColonyState       _wild;
    private readonly ColonyFood        _food;
    private readonly EvolutionService  _evolution;
    private readonly EntitySpatialGrid _grid;

    private readonly MotionSystem         _motion;
    private readonly InstinctSystem       _instincts;
    private readonly SurveySystem         _survey;
    private readonly UtilityPriorSystem   _utility;
    private readonly CasteThresholdSystem _thresholds;
    private readonly SkillGraphStore     _skills;
    private readonly BoidSystem           _boids;
    private readonly EnergyLedger         _ledger;
    private readonly SpeechBus            _speech;
    private readonly BehaviourDescriptorLog _descriptors;
    private readonly NoveltyProgressSystem  _novelty;

    public LifeEventLog LifeLog { get; set; }

    private readonly Queue<EntityModel> _pendingDeath = new();
    private EntityModel[] _deferredDispose = Array.Empty<EntityModel>();
    private int _deferredCount;
    private int _nextEntityId;
    private double _nextPredatorTime;

    private readonly EntityModel[] _witnesses = new EntityModel[InfectionService.MaxNeighbors];

    public event Action<EntityModel> Spawned;
    public event Action<EntityModel> Died;
    public event Action<EntityModel> TickUrgent;

    internal void RestoreLastId(int id) => _nextEntityId = id;
    public int LastId => _nextEntityId;
    public int CivilCount => _colonies.Count - (_wild != null ? 1 : 0);
    public int CivilPopulation => _entities.Count - (_wild != null ? _wild.MemberCount : 0);

    public bool HasFreeSlot(ColonyState colony)
    {
        if (colony == null || _entities.Count >= _capacity) return false;
        return colony.IsWild
            ? colony.MemberCount < SimulationRules.Active.PredatorCount
            : CivilPopulation < _maxPopulation;
    }

    public ColonyPopulation(GameObject prefab, Transform parent, EntityTuning tuning, int maxPopulation, int capacity, float spawnRadius,
        List<EntityModel> entities, ColonyRegistry colonies, ColonyState wild, ColonyFood food, EvolutionService evolution,
        EntitySpatialGrid grid, MotionSystem motion, InstinctSystem instincts, SurveySystem survey, UtilityPriorSystem utility,
        CasteThresholdSystem thresholds, SkillGraphStore skills, BoidSystem boids, EnergyLedger ledger, SpeechBus speech, bool spawnNetworkObjects,
        BehaviourDescriptorLog descriptors = null, NoveltyProgressSystem novelty = null)
    {
        _spawnNetworkObjects = spawnNetworkObjects;
        _prefab        = prefab;
        _parent        = parent;
        _tuning        = tuning;
        _maxPopulation = maxPopulation;
        _capacity      = capacity;
        _spawnRadius   = spawnRadius;
        _entities      = entities;
        _colonies      = colonies;
        _wild          = wild;
        _food          = food;
        _evolution     = evolution;
        _grid          = grid;
        _motion        = motion;
        _instincts     = instincts;
        _survey        = survey;
        _utility       = utility;
        _thresholds    = thresholds;
        _skills        = skills;
        _boids         = boids;
        _ledger        = ledger;
        _speech        = speech;
        _descriptors   = descriptors;
        _novelty       = novelty;
    }

    public void StartPredatorClock(double now) => _nextPredatorTime = now;

    public EntityModel SpawnEntity(Vector3 position, ColonyState colony, EntityBrainContext inheritedCtx = null, EntityModel parent = null, EntityModel mate = null, float2? birthVitals = null,
        TheRavine.Base.PopulationSnapshot.Agent? saved = null)
    {
        if (!TheRavine.Base.WorldAuthority.IsAuthority || !HasFreeSlot(colony) ||
            (parent != null && !birthVitals.HasValue && SimulationRules.Active.ReproductionTransferEnabled))
        {
            inheritedCtx?.Dispose();
            return null;
        }

        var ctx = inheritedCtx ?? colony.Brain.CreateContext();
        var model = new EntityModel();
        GameObject go = null;
        try
        {
            go = UnityEngine.Object.Instantiate(_prefab, position, Quaternion.identity, _parent);
            RegisterEntity(go, model, position, colony, ctx, parent, mate, birthVitals, saved);
            return model;
        }
        catch
        {
            if (_entities.Contains(model)) RemoveEntitySwapBack(model);
            if (model.Brain == null) ctx.Dispose();
            model.Dispose();
            if (go != null) UnityEngine.Object.Destroy(go);
            throw;
        }
    }

    private void RegisterEntity(GameObject go, EntityModel model, Vector3 position, ColonyState colony, EntityBrainContext ctx,
        EntityModel parent, EntityModel mate, float2? birthVitals, TheRavine.Base.PopulationSnapshot.Agent? saved)
    {
        foreach (var expressible in go.GetComponents<IGeneticPhenotype>())
            expressible.ApplyGeneticPhenotype(ctx.CoordMLP.Params);

        var netObj = go.GetComponent<NetworkObject>();
        var network = NetworkManager.Singleton;
        if (_spawnNetworkObjects && netObj != null && network != null && network.IsServer) netObj.Spawn();

        var viewModel = go.GetComponent<EntityViewModel>();
        var view      = go.GetComponent<EntityView>();

        viewModel.BindMotion(_motion);

        model.AssignId(saved?.Id ?? ++_nextEntityId);

        model.Configure(colony, ctx, viewModel, viewModel, go, _tuning, parent);
        if (birthVitals.HasValue)
        {
            model.Stats.En = birthVitals.Value.x;
            model.Stats.Hp = birthVitals.Value.y;
        }
        model.Init();
        viewModel.Initialize(model);
        view.Initialize(viewModel);
        model.VisualCulling = new VisualCullingComponent(go, view.LabelObject);
        model.SetUp();

        float startEnergy = model.BodyEnergy;
        if (saved.HasValue) model.RestoreCondition(saved.Value);
        else if (parent != null)
        {
            float energyCost = parent.Tuning.ReproduceEnergyCost;
            parent.Stats.En -= energyCost;
            parent.Stats.Hp -= parent.Tuning.ReproduceHealthCost;
            if (birthVitals.HasValue)
            {
                _ledger.Flows.BirthTransfer += startEnergy;
                _ledger.Flows.Reproduce += energyCost - startEnergy;
            }
            else
            {
                _ledger.Flows.ChildStart += startEnergy;
                _ledger.Flows.Reproduce += energyCost;
            }
        }
        else _ledger.Flows.SpawnStart += startEnergy;

        model.Mortality.Died += () => HandleEntityDied(model);
        model.OnReproduceRequest += SpawnChild;
        model.OnUrgentTick += RaiseTickUrgent;

        model.ManagerIndex = _entities.Count;
        _motion.SetPosition(model.ManagerIndex, position);
        _entities.Add(model);
        colony.AddMember(model);
        model.SyncCount();
        if (!saved.HasValue) colony.Stats.RecordBirth();
        _instincts.Reset(model.ManagerIndex, InstinctGenes.From(in ctx.CoordMLP.Params));
        _utility.Write(model.ManagerIndex, default);
        _thresholds.Reset(model.ManagerIndex, CasteThresholdSystem.Initial());
        _skills.Reset(model.ManagerIndex, in ctx.CoordMLP.Params, colony.SkillTopology, SkillAtlas.Active);
        _skills.SetColony(model.ManagerIndex, colony.Index);
        ref readonly var rules = ref SimulationRules.Frame;
        bool muted = rules.SpeechMuteSampling && !colony.IsWild && RavineRandom.RangeFloat() < rules.SpeechMuteFraction;
        model.Speech.SetMuted(muted);
        _speech.Reset(model.ManagerIndex, model.ColonyIndex, model.EntityId, muted, colony.Arm);
        _descriptors?.Reset(model.ManagerIndex);
        _novelty?.Reset(model.ManagerIndex);
        if (!saved.HasValue)
        {
            LifeLog?.Birth(model, parent, mate);
            Spawned?.Invoke(model);
        }
    }

    public bool SpawnChild(EntityModel parent)
    {
        var colony = parent.Colony;
        if (parent.IsWild || colony == null) return false;
        var rules = SimulationRules.Active;
        if (!HasFreeSlot(colony)) return RejectBirth(colony, ColonyStats.BirthFailure.Capacity);
        float energyCost = parent.Tuning.ReproduceEnergyCost;
        float healthCost = parent.Tuning.ReproduceHealthCost;
        float reserve = rules.ReproductionTransferEnabled ? rules.BirthParentReserve : 0f;
        float remainingEnergy = parent.Stats.En - energyCost;
        float remainingHealth = parent.Stats.Hp - healthCost;
        if (!(remainingEnergy >= reserve * parent.Stats.MaxEnergy && remainingHealth >= reserve * parent.Stats.MaxHealth) ||
            (rules.ReproductionTransferEnabled && (remainingEnergy <= 0f || remainingHealth <= 0f)))
            return RejectBirth(colony, ColonyStats.BirthFailure.ParentReserve);

        var parentParams = parent.Brain.Context.CoordMLP.Params;
        var mate         = _evolution.SelectMate(parent);

        var parentVirology = parent.Virology;
        if (mate != null && parentVirology != null
            && parentVirology.Modifiers.CytoIncompat >= SimulationRules.Active.CytoIncompatThreshold
            && mate.LineageId != parent.LineageId)
            return RejectBirth(colony, ColonyStats.BirthFailure.Incompatibility);

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

        float2? birthVitals = null;
        if (rules.ReproductionTransferEnabled)
        {
            var child = EntityTuning.Express(in _tuning, in childParams, CasteModifiers.For(Caste.Worker));
            float2 limits = new(child.MaxEnergy, child.MaxHealth * rules.JuvenileHealthMul);
            if (!TryBirthTransfer(new float2(energyCost, healthCost), limits, rules, out float2 transferred))
                return RejectBirth(colony, ColonyStats.BirthFailure.ChildReserve);
            birthVitals = transferred;
        }

        var childCtx = colony.Brain.CreateContext(childParams);
        var pos      = parent.Motor.Position()
                     + (Vector3)RavineRandom.GetInsideCircle().normalized * rules.ChildSpawnOffset
                     + Vector3.up * rules.ChildSpawnLift;

        return SpawnEntity(pos, colony, childCtx, parent, mate, birthVitals) != null
            || RejectBirth(colony, ColonyStats.BirthFailure.Spawn);
    }

    public static bool TryBirthTransfer(float2 costs, float2 childLimits, SimulationRules rules, out float2 transfer)
    {
        transfer = math.min(childLimits, costs * new float2(rules.BirthEnergyTransfer, rules.BirthHealthTransfer));
        return math.all(math.isfinite(transfer)) && math.all(transfer > 0f)
            && math.all(transfer >= childLimits * rules.BirthChildReserve);
    }

    private static bool RejectBirth(ColonyState colony, ColonyStats.BirthFailure reason)
    {
        colony.Stats.BirthFailures[(int)reason]++;
        return false;
    }

    public Vector3 RandomPosition(ColonyState colony)
    {
        var v    = RavineRandom.GetInsideCircle(_spawnRadius);
        var home = colony.Nest.Position;
        return new Vector3(home.x + v.x, _parent.position.y, home.y + v.y);
    }

    private void RaiseTickUrgent(EntityModel model) => TickUrgent?.Invoke(model);

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
            model.DropHeldObject(model.DeathPosition);
            _food.DropCorpse(model, cause);
            model.Brain?.CompleteTerminal(_evolution.TerminalPenaltyFor(model, cause), SimulationClock.Time);
            LifeLog?.Death(model, cause);
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

        if (cause == DeathCause.Killed) colony.RaisePressure(r.PressureFromKill);
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

    internal void DiscardEntities()
    {
        while (_entities.Count > 0)
        {
            var model = _entities[_entities.Count - 1];
            RemoveEntitySwapBack(model);
            model.Dispose();
            if (model.SelfObject != null) UnityEngine.Object.Destroy(model.SelfObject);
        }
    }

    private void RemoveEntitySwapBack(EntityModel model)
    {
        int idx = model.ManagerIndex;

        if ((uint)idx >= (uint)_entities.Count || !ReferenceEquals(_entities[idx], model))
        {
            idx = _entities.IndexOf(model);
            if (idx < 0) { model.ManagerIndex = -1; return; }
        }

        model.ClearCount();
        model.Colony?.RemoveMember(model, _entities);
        model.Motor.BindSlot(-1);

        int last = _entities.Count - 1;
        _motion.RemoveSwapBack(idx, last);
        if (idx != last)
        {
            var moved = _entities[last];
            _entities[idx]    = moved;
            moved.ManagerIndex = idx;
            moved.Colony?.RemapMember(moved.ColonySlot, idx);
        }

        _instincts.RemoveSwapBack(idx, last);
        _survey.RemoveSwapBack(idx, last);
        _boids.RemoveSwapBack(idx, last);
        _utility.RemoveSwapBack(idx, last);
        _thresholds.RemoveSwapBack(idx, last);
        _skills.RemoveSwapBack(idx, last);
        _speech.RemoveSwapBack(idx, last);
        _descriptors?.RemoveSwapBack(idx, last);
        _novelty?.RemoveSwapBack(idx, last);
        _grid.MarkStale();
        _entities.RemoveAt(last);
        model.ManagerIndex = -1;
        model.GridSlot = -1;
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
            ReseedColony(colony, PickDonor(_colonies, colony));
        }
    }

    public static ColonyState PickDonor(ColonyRegistry colonies, ColonyState exclude)
    {
        ColonyState best = null;
        float bestScore = float.MinValue;
        for (int c = 0; c < colonies.Count; c++)
        {
            var colony = colonies[c];
            if (colony == exclude || colony.IsWild || colony.MemberCount == 0 || colony.Arm != exclude.Arm) continue;
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
            colony.InheritGenome(donor.Genome);
            var old = colony.Brain;
            colony.ReplaceBrain(new SharedHierarchicalBrain(donor.Brain));
            old?.Dispose();
        }

        int count = SimulationRules.Active.ColonyRespawnCount;
        var donorMembers = donor != null ? donor.Members : default;
        for (int i = 0; i < count && HasFreeSlot(colony); i++)
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

        var members = wild.Members;
        float2 home;
        float  spread;
        if (members.Length > 0)
        {
            home   = _entities[members[RavineRandom.RangeInt(0, members.Length)]].Position2D;
            spread = r.PredatorPackSpread;
        }
        else
        {
            int civil = RavineRandom.RangeInt(0, CivilCount);
            home = _colonies[civil < wild.Index ? civil : civil + 1].Nest.Position;
            spread = r.PredatorSpawnDistance;
        }
        float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
        math.sincos(a, out float sa, out float ca);
        var p = home + new float2(ca, sa) * spread;
        var ctx = members.Length > 0
            ? wild.Brain.CreateContext(_entities[members[RavineRandom.RangeInt(0, members.Length)]].Brain.Context.CoordMLP.Params.GetMutatedGeneticParameters())
            : null;
        SpawnEntity(new Vector3(p.x, _parent.position.y, p.y), wild, ctx);
    }
}
