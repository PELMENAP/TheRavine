using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public unsafe struct SpeechCredit
{
    public int   Speaker;
    public int   SpeakerId;
    public float Time;
    public int   Count;
    public fixed int   Listener[SimulationRules.SpeechCreditListeners];
    public fixed int   ListenerId[SimulationRules.SpeechCreditListeners];
    public fixed float Fitness[SimulationRules.SpeechCreditListeners];
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public unsafe struct SpeechDeliverJob : IJob
{
    [ReadOnly] public NativeArray<Utterance> Emissions;
    public int Count;

    [ReadOnly] public NativeArray<int>   CellEnd;
    [ReadOnly] public NativeArray<int>   Items;
    [ReadOnly] public NativeArray<int>   Ids;
    [ReadOnly] public NativeArray<float> Px;
    [ReadOnly] public NativeArray<float> Pz;
    public NativeArray<int> Visit;
    public int   Mask;
    public float InvCell;
    public int   EpochBase;

    public float BaseRadius;
    public float MinFraction;
    public float Exponent;

    [ReadOnly] public NativeArray<byte>  Muted;
    [ReadOnly] public NativeArray<int>   Colony;
    [ReadOnly] public NativeArray<int>   EntityId;
    [ReadOnly] public NativeArray<float> Fitness;

    public NativeArray<Utterance>    Inbox;
    public NativeArray<byte>         InboxHead;
    public NativeArray<SpeechCredit> Credits;
    public int CreditBase;
    public int CreditMask;
    public NativeArray<int> Delivered;

    public void Execute()
    {
        int capacity  = Muted.Length;
        int delivered = 0;
        var credits   = (SpeechCredit*)Credits.GetUnsafePtr();

        for (int e = 0; e < Count; e++)
        {
            var u = Emissions[e];
            if (u.Len == 0 || u.Loudness == 0) continue;

            float radius = BaseRadius * math.lerp(MinFraction, 1f, u.Loudness * (1f / 255f));
            if (radius <= 0f) continue;

            float r2   = radius * radius;
            float invR = 1f / radius;
            int ring   = math.max(1, (int)math.ceil(radius * InvCell));
            int cx     = (int)math.floor(u.Pos.x * InvCell);
            int cz     = (int)math.floor(u.Pos.y * InvCell);
            int epoch  = EpochBase + e + 1;
            int speakerColony = (uint)u.Speaker < (uint)capacity ? Colony[u.Speaker] : -1;
            var credit = credits + ((CreditBase + e) & CreditMask);

            for (int dz = -ring; dz <= ring; dz++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                int b = EntitySpatialGrid.HashCell(cx + dx, cz + dz, Mask);
                if (Visit[b] == epoch) continue;
                Visit[b] = epoch;

                int s = b == 0 ? 0 : CellEnd[b - 1];
                int end = CellEnd[b];
                for (int k = s; k < end; k++)
                {
                    int i  = Items[k];
                    float ddx = Px[i] - u.Pos.x;
                    float ddz = Pz[i] - u.Pos.y;
                    float d2  = ddx * ddx + ddz * ddz;
                    if (d2 > r2) continue;

                    int listener = Ids[i];
                    if ((uint)listener >= (uint)capacity || listener == u.Speaker || Muted[listener] != 0) continue;

                    float att   = math.pow(math.saturate(1f - math.sqrt(d2) * invR), Exponent);
                    int   heard = (int)(u.Loudness * att + 0.5f);
                    if (heard <= 0) continue;

                    var copy = u;
                    copy.Loudness = (byte)heard;
                    copy.Flags    = (byte)((u.Flags & ~Utterance.FlagKin)
                                    | (speakerColony >= 0 && Colony[listener] == speakerColony ? Utterance.FlagKin : 0));

                    int slot = InboxHead[listener];
                    Inbox[listener * SimulationRules.SpeechInbox + slot] = copy;
                    InboxHead[listener] = (byte)((slot + 1) & (SimulationRules.SpeechInbox - 1));
                    delivered++;

                    int n = credit->Count;
                    if (n < SimulationRules.SpeechCreditListeners)
                    {
                        credit->Listener[n]   = listener;
                        credit->ListenerId[n] = EntityId[listener];
                        credit->Fitness[n]    = Fitness[listener];
                        credit->Count         = n + 1;
                    }
                }
            }
        }

        Delivered[0] += delivered;
    }
}

public sealed class SpeechBus : IDisposable
{
    private const int CreditMask = SimulationRules.SpeechCreditRing - 1;

    private readonly int _capacity;
    private readonly List<EntityModel> _entities;

    private NativeList<Utterance>    _pending;
    private NativeList<Utterance>    _batch;
    private NativeList<Utterance>    _audible;
    private NativeArray<Utterance>   _inbox;
    private NativeArray<byte>        _inboxHead;
    private NativeArray<byte>        _muted;
    private NativeArray<int>         _colony;
    private NativeArray<int>         _entityId;
    private NativeArray<float>       _fitness;
    private NativeArray<SpeechCredit> _credits;
    private NativeArray<int>         _delivered;

    private int  _creditHead;
    private int  _creditTail;
    private float _baseline;

    public int Capacity => _capacity;
    public int PendingCount => _pending.IsCreated ? _pending.Length : 0;
    private int _audibleUsers;
    public NativeList<Utterance> Audible => _audible;
    public long Emitted   { get; private set; }
    public long Dropped   { get; private set; }
    public long Delivered => _delivered.IsCreated ? _delivered[0] : 0;
    public long Credited  { get; private set; }
    public float CreditBaseline => _baseline;

    public SpeechBus(int capacity, List<EntityModel> entities)
    {
        _capacity = math.max(capacity, 1);
        _entities = entities;

        int emitCap = _capacity * 2 + 16;
        _pending   = new NativeList<Utterance>(emitCap, Allocator.Persistent);
        _batch     = new NativeList<Utterance>(emitCap, Allocator.Persistent);
        _audible   = new NativeList<Utterance>(emitCap, Allocator.Persistent);
        _inbox     = new NativeArray<Utterance>(_capacity * SimulationRules.SpeechInbox, Allocator.Persistent);
        _inboxHead = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _muted     = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _colony    = new NativeArray<int>(_capacity, Allocator.Persistent);
        _entityId  = new NativeArray<int>(_capacity, Allocator.Persistent);
        _fitness   = new NativeArray<float>(_capacity, Allocator.Persistent);
        _credits   = new NativeArray<SpeechCredit>(SimulationRules.SpeechCreditRing, Allocator.Persistent);
        _delivered = new NativeArray<int>(1, Allocator.Persistent);
    }

    public void Reset(int index, int colony, int entityId, bool muted)
    {
        if ((uint)index >= (uint)_capacity) return;
        ClearRow(index);
        _colony[index]   = colony;
        _entityId[index] = entityId;
        _muted[index]    = muted ? (byte)1 : (byte)0;
    }

    public void SetFitness(int index, float fitness)
    {
        if ((uint)index < (uint)_capacity) _fitness[index] = fitness;
    }

    public bool Emit(in Utterance utterance)
    {
        if (!_pending.IsCreated || _pending.Length >= _pending.Capacity)
        {
            Dropped++;
            return false;
        }
        _pending.AddNoResize(utterance);
        Emitted++;
        return true;
    }

    public static bool TryStartCooldown(ref double readyTime)
    {
        double now = SimulationClock.TimeD;
        if (now < readyTime) return false;
        readyTime = now + SimulationRules.Frame.SpeechCooldown;
        return true;
    }

    public Utterance InboxAt(int index, int slot)
        => (uint)index < (uint)_capacity && (uint)slot < (uint)SimulationRules.SpeechInbox
            ? _inbox[index * SimulationRules.SpeechInbox + slot]
            : default;

    public void Flush(EntitySpatialGrid grid, double now)
    {
        if (!_pending.IsCreated) return;
        if (_pending.Length > 0 && grid != null && grid.IsReady) Deliver(grid);
        Settle((float)now);
    }

    private void Deliver(EntitySpatialGrid grid)
    {
        (_pending, _batch) = (_batch, _pending);
        _pending.Clear();

        int n = _batch.Length;
        int baseSlot = _creditHead;
        for (int e = 0; e < n; e++)
        {
            var u = _batch[e];
            bool credited = !u.IsReflex && (uint)u.Speaker < (uint)_capacity;
            _credits[(baseSlot + e) & CreditMask] = new SpeechCredit
            {
                Speaker   = credited ? u.Speaker : Utterance.External,
                SpeakerId = credited ? _entityId[u.Speaker] : 0,
                Time      = u.Time,
            };
        }
        if (_audibleUsers > 0)
        {
            int room = _audible.Capacity - _audible.Length;
            for (int e = 0; e < n && e < room; e++) _audible.AddNoResize(_batch[e]);
        }

        _creditHead += n;
        if (_creditHead - _creditTail > SimulationRules.SpeechCreditRing)
            _creditTail = _creditHead - SimulationRules.SpeechCreditRing;

        ref readonly var r = ref SimulationRules.Frame;
        new SpeechDeliverJob
        {
            Emissions   = _batch.AsArray(),
            Count       = n,
            CellEnd     = grid.CellEnd,
            Items       = grid.Items,
            Ids         = grid.Ids,
            Px          = grid.Px,
            Pz          = grid.Pz,
            Visit       = grid.Visit,
            Mask        = grid.BucketMask,
            InvCell     = grid.InvCell,
            EpochBase   = grid.ReserveEpochs(n),
            BaseRadius  = r.SpeechRadius,
            MinFraction = r.SpeechMinRadiusFraction,
            Exponent    = r.SpeechAttenuationExp,
            Muted       = _muted,
            Colony      = _colony,
            EntityId    = _entityId,
            Fitness     = _fitness,
            Inbox       = _inbox,
            InboxHead   = _inboxHead,
            Credits     = _credits,
            CreditBase  = baseSlot,
            CreditMask  = CreditMask,
            Delivered   = _delivered,
        }.Run();
    }

    public void RetainAudible() => _audibleUsers++;

    public void ReleaseAudible()
    {
        if (_audibleUsers > 0) _audibleUsers--;
        if (_audibleUsers == 0) ClearAudible();
    }

    public void ClearAudible()
    {
        if (_audible.IsCreated) _audible.Clear();
    }

    private unsafe void Settle(float now)
    {
        ref readonly var r = ref SimulationRules.Frame;
        float window = r.SpeechCreditWindow;
        var credits  = (SpeechCredit*)_credits.GetUnsafePtr();

        while (_creditTail != _creditHead)
        {
            var c = credits + (_creditTail & CreditMask);
            if (now - c->Time < window) break;
            _creditTail++;

            float sum = 0f;
            int   n   = 0;
            for (int i = 0; i < c->Count; i++)
            {
                int idx = c->Listener[i];
                if ((uint)idx >= (uint)_capacity || _entityId[idx] != c->ListenerId[i]) continue;
                sum += _fitness[idx] - c->Fitness[i];
                n++;
            }
            if (n == 0) continue;

            float mean = sum / n;
            float adv  = mean - _baseline;
            _baseline += r.SpeechCreditBaselineAlpha * adv;

            int speaker = c->Speaker;
            if ((uint)speaker >= (uint)_capacity || _entityId[speaker] != c->SpeakerId || speaker >= _entities.Count) continue;
            var model = _entities[speaker];
            if (model == null || model.IsDisposed) continue;

            model.CreditSpeech(r.SpeechCreditWeight * adv);
            Credited++;
        }
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)_capacity || (uint)last >= (uint)_capacity) return;
        if (index != last)
        {
            int dst = index * SimulationRules.SpeechInbox;
            int src = last * SimulationRules.SpeechInbox;
            for (int s = 0; s < SimulationRules.SpeechInbox; s++) _inbox[dst + s] = _inbox[src + s];
            _inboxHead[index] = _inboxHead[last];
            _muted[index]     = _muted[last];
            _colony[index]    = _colony[last];
            _entityId[index]  = _entityId[last];
            _fitness[index]   = _fitness[last];
        }
        ClearRow(last);
    }

    private void ClearRow(int index)
    {
        int row = index * SimulationRules.SpeechInbox;
        for (int s = 0; s < SimulationRules.SpeechInbox; s++) _inbox[row + s] = default;
        _inboxHead[index] = 0;
        _muted[index]     = 0;
        _colony[index]    = 0;
        _entityId[index]  = 0;
        _fitness[index]   = 0f;
    }

    public void Dispose()
    {
        if (_pending.IsCreated)    _pending.Dispose();
        if (_batch.IsCreated)      _batch.Dispose();
        if (_audible.IsCreated)    _audible.Dispose();
        if (_inbox.IsCreated)      _inbox.Dispose();
        if (_inboxHead.IsCreated)  _inboxHead.Dispose();
        if (_muted.IsCreated)      _muted.Dispose();
        if (_colony.IsCreated)     _colony.Dispose();
        if (_entityId.IsCreated)   _entityId.Dispose();
        if (_fitness.IsCreated)    _fitness.Dispose();
        if (_credits.IsCreated)    _credits.Dispose();
        if (_delivered.IsCreated)  _delivered.Dispose();
    }
}
