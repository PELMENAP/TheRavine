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
    private readonly HelpLog _helpLog;
    private readonly BehaviourMotifLog _motifs;
    private readonly KnowledgeSystem _knowledge;

    public LifeEventLog LifeLog { get; set; }
    public LineageStore Lineage { get; } = new(SimulationRules.Active.LineageCapacity);

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
        BehaviourDescriptorLog descriptors = null, NoveltyProgressSystem novelty = null, HelpLog helpLog = null, BehaviourMotifLog motifs = null,
        KnowledgeSystem knowledge = null)
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
        _helpLog       = helpLog;
        _motifs        = motifs;
        _knowledge     = knowledge;
    }

    public void StartPredatorClock(double now) => _nextPredatorTime = now;

    public EntityModel SpawnEntity(Vector3 position, ColonyState colony, EntityBrainContext inheritedCtx = null, EntityModel parent = null, EntityModel mate = null, float2? birthVitals = null,
        TheRavine.Base.PopulationSnapshot.Agent? saved = null, uint mateLoci = 0u)
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
            RegisterEntity(go, model, position, colony, ctx, parent, mate, birthVitals, saved, mateLoci);
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
        EntityModel parent, EntityModel mate, float2? birthVitals, TheRavine.Base.PopulationSnapshot.Agent? saved, uint mateLoci)
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
        model.SetLineage(saved?.Mate ?? mate?.EntityId ?? 0, saved?.MateLoci ?? mateLoci);
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
        _motifs?.Reset(model.ManagerIndex);
        _novelty?.Reset(model.ManagerIndex);
        _helpLog?.Reset(model.ManagerIndex);
        _knowledge?.Reset(model.ManagerIndex);
        if (saved.HasValue) model.RestoreHelp(saved.Value);
        Lineage.Birth(model.EntityId, saved?.Parent ?? parent?.EntityId ?? 0, model.MateId,
            SimulationClock.Time - (saved?.Age ?? 0f), model.MateLoci);
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

        uint childLoci = 0u;
        if (rules.MateLociEnabled)
        {
            childLoci = MateLoci.Mutate(mate != null ? MateLoci.Cross(parent.MateLoci, mate.MateLoci) : parent.MateLoci,
                rules.MateLocusMutation);
            float viability = MateLoci.Viability(MateLoci.Conflicts(childLoci, rules.MateLociPairs), rules.MateConflictLethality);
            if (viability < 1f && RavineRandom.RangeFloat() >= viability)
            {
                parent.Stats.En -= energyCost;
                _ledger.Flows.Reproduce += energyCost;
                return RejectBirth(colony, ColonyStats.BirthFailure.Inviable);
            }
        }

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

        return SpawnEntity(pos, colony, childCtx, parent, mate, birthVitals, null, childLoci) != null
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
            if (model.IsGuest) model.Colony?.Stats.RecordGuestDeath(model.TimeAlive);
            HandleLeaderDeath(model);
            BroadcastDeath(model, cause);
            model.DropHeldObject(model.DeathPosition);
            _food.DropCorpse(model, cause);
            model.Brain?.CompleteTerminal(_evolution.TerminalPenaltyFor(model, cause), SimulationClock.Time);
            LifeLog?.Death(model, cause);
            Lineage.Death(model.EntityId, SimulationClock.Time);
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
        if (model.IsGuest && model.Colony != null) model.Colony.GuestCount--;
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
        _motifs?.RemoveSwapBack(idx, last);
        _novelty?.RemoveSwapBack(idx, last);
        _helpLog?.RemoveSwapBack(idx, last);
        _knowledge?.RemoveSwapBack(idx, last);
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
            var parent = r.FissionEnabled ? PickFissionParent(_colonies, colony, r.FissionMembers) : null;
            ReseedColony(colony, parent ?? PickDonor(_colonies, colony), parent != null);
        }
    }

    public static ColonyState PickFissionParent(ColonyRegistry colonies, ColonyState exclude, int minAdults)
    {
        ColonyState best = null;
        int bestAdults = minAdults;
        for (int c = 0; c < colonies.Count; c++)
        {
            var colony = colonies[c];
            if (colony == exclude || colony.IsWild || colony.Arm != exclude.Arm) continue;
            int adults = colony.AdultCount;
            if (adults <= bestAdults) continue;
            bestAdults = adults;
            best = colony;
        }
        return best;
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

    private readonly List<EntityModel> _moveScratch = new();

    private void ReseedColony(ColonyState colony, ColonyState donor, bool fission)
    {
        var rules = SimulationRules.Active;
        var mode  = rules.ReseedMode;
        colony.ExtinctSince = double.NaN;
        colony.EmaPrimed    = false;
        colony.Stats.Reseeds++;

        string origin = (fission ? "fission" : "reseeded") + (mode == ReseedMode.MutatedClone ? "_mutated" : mode == ReseedMode.Fresh ? "_fresh" : "");
        if (mode == ReseedMode.Fresh)
        {
            var old = colony.Brain;
            colony.ResetGenome();
            ReplaceColonyBrain(colony, new SharedHierarchicalBrain(old.InputSize, old.LstmHidden, old.WideSocialExecutor), origin, 0);
        }
        else if (donor != null)
        {
            colony.InheritGenome(donor.Genome);
            var brain = new SharedHierarchicalBrain(donor.Brain);
            if (mode == ReseedMode.MutatedClone) brain.AddWeightNoise(rules.ReseedWeightSigma);
            ReplaceColonyBrain(colony, brain, origin, donor.ColonyId);
        }

        if (fission && donor != null && MoveFissionShare(donor, colony, rules.FissionShare) > 0) return;

        int count = rules.ColonyRespawnCount;
        var donorMembers = donor != null && mode != ReseedMode.Fresh ? donor.Members : default;
        for (int i = 0; i < count && HasFreeSlot(colony); i++)
        {
            GeneticParameters genes = donorMembers.Length > 0
                ? _entities[donorMembers[RavineRandom.RangeInt(0, donorMembers.Length)]].Brain.Context.CoordMLP.Params.GetMutatedGeneticParameters()
                : GeneticParameters.Default;
            SpawnEntity(RandomPosition(colony), colony, colony.Brain.CreateContext(genes));
            if (donor != null) donorMembers = donor.Members;
        }
    }

    private void ReplaceColonyBrain(ColonyState colony, SharedHierarchicalBrain brain, string origin, int donorColonyId)
    {
        var old = colony.Brain;
        colony.ReplaceBrain(brain, origin, donorColonyId);
        if (old == null) return;

        for (int i = 0; i < _entities.Count; i++)
        {
            var e = _entities[i];
            if (e.Brain != null && e.Brain.Uses(old)) e.Brain.ReplaceBrain(brain);
        }
        old.Dispose();
    }

    private int MoveFissionShare(ColonyState parent, ColonyState child, float share)
    {
        _moveScratch.Clear();
        var members = parent.Members;
        for (int i = 0; i < members.Length; i++)
        {
            var e = _entities[members[i]];
            if (!e.IsDisposed && !e.IsDeathPending) _moveScratch.Add(e);
        }

        int total = _moveScratch.Count;
        int want = math.clamp((int)math.round(total * share), 0, total - 1);
        int moved = 0;
        for (int k = 0; k < want; k++)
        {
            int pick = RavineRandom.RangeInt(k, total);
            (_moveScratch[k], _moveScratch[pick]) = (_moveScratch[pick], _moveScratch[k]);
            if (MoveMember(_moveScratch[k], child, true)) moved++;
        }
        _moveScratch.Clear();
        return moved;
    }

    private bool[] _transplantDone = Array.Empty<bool>();

    public void TickTransplants()
    {
        var schedule = SimulationRules.Active.TransplantSchedule;
        if (schedule.Length == 0) return;
        if (_transplantDone.Length != schedule.Length) _transplantDone = new bool[schedule.Length];

        double now = SimulationClock.TimeD;
        for (int i = 0; i < schedule.Length; i++)
        {
            if (_transplantDone[i] || now < schedule[i].time) continue;
            _transplantDone[i] = true;
            Transplant(schedule[i]);
        }
    }

    private int Transplant(in TransplantEntry entry)
    {
        if (!_colonies.TryGetById(entry.fromColonyId, out var origin) || !_colonies.TryGetById(entry.toColonyId, out var to)
            || ReferenceEquals(origin, to) || origin.IsWild || to.IsWild) return 0;

        _moveScratch.Clear();
        var members = origin.Members;
        for (int i = 0; i < members.Length; i++)
        {
            var e = _entities[members[i]];
            if (!e.IsDisposed && !e.IsDeathPending && e.CasteFixed && !e.IsJuvenile && !e.IsGuest) _moveScratch.Add(e);
        }

        int total = _moveScratch.Count;
        int want = math.min(entry.count, total);
        int moved = 0;
        for (int k = 0; k < want; k++)
        {
            int pick = RavineRandom.RangeInt(k, total);
            (_moveScratch[k], _moveScratch[pick]) = (_moveScratch[pick], _moveScratch[k]);
            if (MoveMember(_moveScratch[k], to, false)) moved++;
        }
        _moveScratch.Clear();
        return moved;
    }

    public bool MoveMember(EntityModel model, ColonyState to, bool bindToTargetBrain)
    {
        var origin = model != null ? model.Colony : null;
        if (origin == null || to == null || ReferenceEquals(origin, to) || origin.IsWild || to.IsWild) return false;
        if (model.IsDisposed || model.IsDeathPending) return false;

        int idx = model.ManagerIndex;
        if ((uint)idx >= (uint)_entities.Count || !ReferenceEquals(_entities[idx], model)) return false;

        if (ReferenceEquals(origin.Leader, model))
        {
            origin.SetLeader(null);
            origin.NextElection = 0d;
        }

        model.ClearCount();
        origin.RemoveMember(model, _entities);
        if (model.IsGuest)
        {
            origin.GuestCount--;
            model.SetGuestOf(0);
        }
        model.Rebind(to);
        to.AddMember(model);
        model.SyncCount();

        var ctx = model.Brain.Context;
        ctx.TrainOff = false;
        if (!bindToTargetBrain)
        {
            model.SetGuestOf(origin.ColonyId);
            to.GuestCount++;
            ctx.TrainOff = !SimulationRules.Active.TransplantTrainGuests;
        }

        _speech.SetColony(idx, to.Index);
        _skills.Reset(idx, in model.Brain.Context.CoordMLP.Params, to.SkillTopology, SkillAtlas.Active);
        _skills.SetColony(idx, to.Index);
        _skills.MarkDirty(idx);
        if (bindToTargetBrain) model.Brain.ReplaceBrain(to.Brain);

        _motion.SetPosition(idx, RandomPosition(to));
        _grid.MarkStale();
        return true;
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
