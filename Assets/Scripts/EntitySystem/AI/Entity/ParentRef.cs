using System;

public readonly struct ParentRef
{
    private readonly WeakReference<EntityModel> _ref;
    public readonly int EntityId;
    public readonly int ColonyId;

    public ParentRef(EntityModel parent)
    {
        _ref     = parent != null ? new WeakReference<EntityModel>(parent) : null;
        EntityId = parent != null ? parent.EntityId : 0;
        ColonyId = parent?.Colony != null ? parent.Colony.ColonyId : 0;
    }

    public bool IsSet => _ref != null;

    public bool TryGet(out EntityModel parent)
    {
        if (_ref != null && _ref.TryGetTarget(out parent)
            && parent.EntityId == EntityId && !parent.IsDisposed && !parent.IsDeathPending)
            return true;

        parent = null;
        return false;
    }
}
