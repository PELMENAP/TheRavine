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

    public ColonyState(ColonyRegistry registry, int index, int colonyId, float2 position, SharedHierarchicalBrain brain, int capacity)
    {
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
