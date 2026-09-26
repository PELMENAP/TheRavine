using System;
using System.Collections.Generic;
using Unity.Mathematics;

public sealed class ColonyState : IDisposable
{
    public readonly int Index;
    public readonly int ColonyId;
    public readonly NestState Nest;
    public SharedHierarchicalBrain Brain { get; private set; }
    public readonly ColonyStats Stats = new();
    public readonly ColonyPoiBoard Pois;
    public readonly float[] LeaderPlanProbs = new float[PlanCatalog.Count];
    public EntityModel Leader { get; private set; }
    public bool HasLeaderPrior { get; private set; }
    public double NextElection;
    public readonly int[] CasteCounts = new int[(int)Caste.Count];
    public int JuvenileCount;
    public readonly bool IsWild;

    public float StorageEma;
    public float PopulationEma;
    public float HungerEma;
    public bool  EmaPrimed;
    public double ExtinctSince = double.NaN;

    public float Score
    {
        get
        {
            ref readonly var r = ref SimulationRules.Frame;
            return StorageEma + PopulationEma / math.max(r.ColonyPopulationNorm, 1f) - HungerEma;
        }
    }

    public void TickEma(float dt)
    {
        ref readonly var r = ref SimulationRules.Frame;
        float storage = math.saturate(Nest.Storage / math.max(r.NestStorageNorm, 1e-3f));
        if (!EmaPrimed)
        {
            StorageEma    = storage;
            PopulationEma = _memberCount;
            HungerEma     = Nest.Hunger;
            EmaPrimed     = true;
            return;
        }
        float k = 1f - math.exp(-math.max(dt, 0f) / math.max(r.ColonyEmaTau, 1e-3f));
        StorageEma    = math.lerp(StorageEma, storage, k);
        PopulationEma = math.lerp(PopulationEma, _memberCount, k);
        HungerEma     = math.lerp(HungerEma, Nest.Hunger, k);
    }

    private float2[] _sitePos;
    private int[]    _siteVotes;
    private double[] _siteUpdated;
    private int[]    _siteVoters;
    private int      _voterStride;

    public void VoteNestSite(float2 position, int voterId)
    {
        ref readonly var r = ref SimulationRules.Frame;
        double now = SimulationClock.TimeD;
        float merge2 = r.QuorumMergeRadius * r.QuorumMergeRadius;
        int free = -1, oldest = 0;

        for (int i = 0; i < _sitePos.Length; i++)
        {
            if (_siteVotes[i] > 0 && now - _siteUpdated[i] > r.QuorumWindow) _siteVotes[i] = 0;
            if (_siteVotes[i] == 0) { if (free < 0) free = i; continue; }
            if (_siteUpdated[i] < _siteUpdated[oldest]) oldest = i;
            if (math.distancesq(_sitePos[i], position) > merge2) continue;

            int baseIdx = i * _voterStride;
            int votes   = _siteVotes[i];
            for (int v = 0; v < votes; v++)
                if (_siteVoters[baseIdx + v] == voterId) { _siteUpdated[i] = now; return; }
            if (votes < _voterStride)
            {
                _siteVoters[baseIdx + votes] = voterId;
                _sitePos[i] = math.lerp(_sitePos[i], position, 1f / (votes + 1));
                _siteVotes[i] = votes + 1;
            }
            _siteUpdated[i] = now;
            return;
        }

        int slot = free >= 0 ? free : oldest;
        _sitePos[slot]     = position;
        _siteVotes[slot]   = 1;
        _siteUpdated[slot] = now;
        _siteVoters[slot * _voterStride] = voterId;
    }

    public bool TryGetQuorumSite(out float2 site)
    {
        ref readonly var r = ref SimulationRules.Frame;
        double now = SimulationClock.TimeD;
        int best = -1;
        for (int i = 0; i < _sitePos.Length; i++)
        {
            if (_siteVotes[i] < math.max(r.QuorumSize, 1) || now - _siteUpdated[i] > r.QuorumWindow) continue;
            if (best < 0 || _siteVotes[i] > _siteVotes[best]) best = i;
        }
        site = best >= 0 ? _sitePos[best] : default;
        return best >= 0;
    }

    public int SiteVotesMax
    {
        get
        {
            int m = 0;
            for (int i = 0; i < _siteVotes.Length; i++) m = math.max(m, _siteVotes[i]);
            return m;
        }
    }

    private void ClearSites()
    {
        Array.Clear(_siteVotes, 0, _siteVotes.Length);
    }

    public void SetLeader(EntityModel leader)
    {
        if (ReferenceEquals(Leader, leader)) return;
        Leader?.SetLeader(false);
        Leader = leader;
        HasLeaderPrior = false;
        leader?.SetLeader(true);
    }

    public void PublishLeaderPrior(System.ReadOnlySpan<float> probs)
    {
        int n = System.Math.Min(probs.Length, LeaderPlanProbs.Length);
        for (int i = 0; i < n; i++) LeaderPlanProbs[i] = probs[i];
        HasLeaderPrior = true;
    }

    private int[] _members;
    private int   _memberCount;

    public int MemberCount => _memberCount;
    public ReadOnlySpan<int> Members => new(_members, 0, _memberCount);

    private readonly ColonyRegistry _registry;

    public ColonyState(ColonyRegistry registry, int index, int colonyId, float2 position, SharedHierarchicalBrain brain, int capacity,
        bool wild = false)
    {
        IsWild = wild;
        var rules = SimulationRules.Active;
        int sites = math.max(rules.QuorumSites, 1);
        _voterStride = math.max(rules.QuorumSize, 1) * 2;
        _sitePos     = new float2[sites];
        _siteVotes   = new int[sites];
        _siteUpdated = new double[sites];
        _siteVoters  = new int[sites * _voterStride];
        _registry = registry;
        Index    = index;
        ColonyId = colonyId;
        Nest     = new NestState(position);
        Pois     = new ColonyPoiBoard(SimulationRules.Active.ColonyPoiCount);
        Brain    = brain;
        _members = new int[math.max(capacity, 4)];
    }

    public void ReplaceBrain(SharedHierarchicalBrain brain) => Brain = brain;

    public void Relocate(float2 position, ChunkFoodIndex food)
    {
        var r = SimulationRules.Active;
        float2 old     = Nest.Position;
        float  storage = Nest.Storage;
        Nest.Storage = 0f;
        Nest.Relocate(position);
        ClearSites();

        if (storage > 0f && food != null)
        {
            float chunk = math.max(r.CacheChunkEnergy, r.CorpseMinEnergy + 1e-3f);
            while (storage > r.CorpseMinEnergy)
            {
                float e = math.min(storage, chunk);
                ViralPayload none = default;
                if (!food.TryAddCorpse(old, e, ref none, false)) break;
                storage -= e;
            }
            Pois.Offer(old, chunk, 0f);
        }

        _registry?.RefreshViews();
    }

    public void AddMember(EntityModel model)
    {
        if (_memberCount == _members.Length) Array.Resize(ref _members, _members.Length << 1);
        model.ColonySlot = _memberCount;
        _members[_memberCount++] = model.ManagerIndex;
    }

    public void RemoveMember(EntityModel model, List<EntityModel> entities)
    {
        int slot = model.ColonySlot;
        model.ColonySlot = -1;
        if ((uint)slot >= (uint)_memberCount) return;

        int last = --_memberCount;
        if (slot != last)
        {
            int moved = _members[last];
            _members[slot] = moved;
            if ((uint)moved < (uint)entities.Count) entities[moved].ColonySlot = slot;
        }
        _members[last] = -1;
    }

    public void RemapMember(int slot, int managerIndex)
    {
        if ((uint)slot < (uint)_memberCount) _members[slot] = managerIndex;
    }

    public void Dispose()
    {
        Nest.Dispose();
        Brain?.Dispose();
        Brain = null;
        _memberCount = 0;
    }
}
