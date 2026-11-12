using System;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using TheRavine.ObjectControl;

[Serializable]
public sealed class ObjectViewMotionSettings
{
    [Min(0)] public int budget = 64;
    [Min(0.01f)] public float popDuration = 0.2f;
    [Min(0.01f)] public float shrinkDuration = 0.15f;
    [Min(0.1f)] public float growthDuration = 4f;
}

public interface IObjectViewMotionHost
{
    void OnGrowthFinished(long viewKey, int cell, PooledObject grown, PoolManager.Pool next, Vector3 nextPosition);
}

public sealed class ObjectViewMotion
{
    public enum Kind : byte { None, Pop, Shrink, Grow }

    private const float HiddenScale = 0.001f;

    public sealed class Slot
    {
        internal readonly Action Complete;
        internal ObjectViewMotion Owner;
        internal int Index;
        internal MotionHandle Handle;
        internal Kind Kind;
        internal PooledObject Object;
        internal long ViewKey;
        internal int Cell;
        internal Vector3 From;
        internal Vector3 To;
        internal PoolManager.Pool NextPool;
        internal Vector3 NextPosition;

        internal Slot(ObjectViewMotion owner, int index)
        {
            Owner = owner;
            Index = index;
            Complete = OnComplete;
        }

        private void OnComplete() => Owner.Finish(this);

        internal void Apply(float t)
        {
            if (Object == null || Object.Transform == null) return;
            Object.Transform.localScale = Vector3.LerpUnclamped(From, To, t);
        }
    }

    private readonly ObjectViewMotionSettings settings;
    private readonly IObjectViewMotionHost host;
    private readonly Slot[] slots;
    private readonly int[] free;
    private readonly Dictionary<PooledObject, Slot> byObject;
    private int freeCount;

    public int ActiveCount => slots.Length - freeCount;

    public ObjectViewMotion(ObjectViewMotionSettings settings, IObjectViewMotionHost host)
    {
        this.settings = settings ?? new ObjectViewMotionSettings();
        this.host = host;

        int capacity = Mathf.Max(0, this.settings.budget);
        slots = new Slot[capacity];
        free = new int[capacity];
        byObject = new Dictionary<PooledObject, Slot>(capacity);
        for (int i = 0; i < capacity; i++)
        {
            slots[i] = new Slot(this, i);
            free[freeCount++] = capacity - 1 - i;
        }
    }

    public bool TryGetSlot(PooledObject obj, out Slot slot)
    {
        if (obj != null && byObject.Count > 0) return byObject.TryGetValue(obj, out slot);
        slot = null;
        return false;
    }

    public bool PopIn(PooledObject obj, long viewKey)
    {
        if (obj == null || byObject.ContainsKey(obj)) return false;
        Slot slot = Acquire(obj);
        if (slot == null) return false;

        slot.Kind = Kind.Pop;
        slot.ViewKey = viewKey;
        slot.From = obj.BaseScale * HiddenScale;
        slot.To = obj.BaseScale;
        Start(slot, settings.popDuration, Ease.OutBack);
        return true;
    }

    public bool ShrinkOut(PooledObject obj, long viewKey)
    {
        if (obj == null) return false;
        Slot slot = Take(obj);
        if (slot == null) return false;

        slot.Kind = Kind.Shrink;
        slot.ViewKey = viewKey;
        slot.From = obj.Transform.localScale;
        slot.To = obj.BaseScale * HiddenScale;
        slot.NextPool = null;
        Start(slot, settings.shrinkDuration, Ease.InQuad);
        return true;
    }

    public bool Grow(PooledObject from, long viewKey, int cell, PoolManager.Pool next, Vector3 nextPosition, Vector3 ratio)
    {
        if (from == null || next == null) return false;
        Slot slot = Take(from);
        if (slot == null) return false;

        slot.Kind = Kind.Grow;
        slot.ViewKey = viewKey;
        slot.Cell = cell;
        slot.From = from.Transform.localScale;
        slot.To = Vector3.Scale(from.BaseScale, ratio);
        slot.NextPool = next;
        slot.NextPosition = nextPosition;
        Start(slot, settings.growthDuration, Ease.OutQuad);
        return true;
    }

    public void Cancel(PooledObject obj)
    {
        if (obj == null || byObject.Count == 0 || !byObject.TryGetValue(obj, out Slot slot)) return;
        Abort(slot);
    }

    public void CancelView(long viewKey)
    {
        if (ActiveCount == 0) return;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot.Kind != Kind.None && slot.ViewKey == viewKey) Abort(slot);
        }
    }

    public void CancelAll()
    {
        if (ActiveCount == 0) return;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].Kind != Kind.None) Abort(slots[i]);
    }

    private Slot Acquire(PooledObject obj)
    {
        if (freeCount == 0) return null;
        Slot slot = slots[free[--freeCount]];
        slot.Object = obj;
        byObject[obj] = slot;
        return slot;
    }

    private Slot Take(PooledObject obj)
    {
        if (!byObject.TryGetValue(obj, out Slot slot)) return Acquire(obj);
        slot.Handle.TryCancel();
        return slot;
    }

    private static void Start(Slot slot, float duration, Ease ease)
    {
        slot.Handle = LMotion.Create(0f, 1f, duration)
            .WithEase(ease)
            .WithImmediateBind()
            .WithOnComplete(slot.Complete)
            .Bind(slot, static (t, s) => s.Apply(t));
    }

    private void Finish(Slot slot)
    {
        PooledObject obj = slot.Object;
        if (obj == null) return;

        Kind kind = slot.Kind;
        long viewKey = slot.ViewKey;
        int cell = slot.Cell;
        PoolManager.Pool next = slot.NextPool;
        Vector3 nextPosition = slot.NextPosition;
        Release(slot);

        if (obj.Transform != null) obj.Transform.localScale = obj.BaseScale;

        if (kind == Kind.Shrink)
            obj.Owner.Release(obj);
        else if (kind == Kind.Grow)
            host.OnGrowthFinished(viewKey, cell, obj, next, nextPosition);
    }

    private void Abort(Slot slot)
    {
        PooledObject obj = slot.Object;
        Kind kind = slot.Kind;
        slot.Handle.TryCancel();
        Release(slot);

        if (obj == null) return;
        if (obj.Transform != null) obj.Transform.localScale = obj.BaseScale;
        if (kind == Kind.Shrink) obj.Owner.Release(obj);
    }

    private void Release(Slot slot)
    {
        if (slot.Object != null) byObject.Remove(slot.Object);
        slot.Object = null;
        slot.NextPool = null;
        slot.Handle = default;
        slot.Kind = Kind.None;
        free[freeCount++] = slot.Index;
    }
}
