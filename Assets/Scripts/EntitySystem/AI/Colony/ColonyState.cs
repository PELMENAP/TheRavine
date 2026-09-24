using System;
using System.Collections.Generic;
using Unity.Mathematics;

public sealed class ColonyState : IDisposable
{
    public readonly int Index;
    public readonly int ColonyId;
    public readonly NestState Nest;
    public SharedHierarchicalBrain Brain { get; private set; }

    private int[] _members;
    private int   _memberCount;

    public int MemberCount => _memberCount;
    public ReadOnlySpan<int> Members => new(_members, 0, _memberCount);

    public ColonyState(int index, int colonyId, float2 position, SharedHierarchicalBrain brain, int capacity)
    {
        Index    = index;
        ColonyId = colonyId;
        Nest     = new NestState(position);
        Brain    = brain;
        _members = new int[math.max(capacity, 4)];
    }

    public void ReplaceBrain(SharedHierarchicalBrain brain) => Brain = brain;

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
