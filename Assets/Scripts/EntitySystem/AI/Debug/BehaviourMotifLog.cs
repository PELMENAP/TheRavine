using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;

using TheRavine.Base;

public sealed class BehaviourMotifLog : IDisposable
{
    public const int Slots          = 32;
    public const int ContextBuckets = 16;
    public const int Actions        = ActionCatalog.Count;
    public const int MotifSpace     = Actions * Actions * Actions * ContextBuckets;

    private const byte   NoAction = byte.MaxValue;
    private const ushort Empty    = ushort.MaxValue;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly int _capacity;
    private readonly int _topK;

    private NativeArray<byte>   _last;
    private NativeArray<byte>   _before;
    private NativeArray<byte>   _lastBucket;
    private NativeArray<byte>   _beforeBucket;
    private NativeArray<ushort> _keys;
    private NativeArray<ushort> _counts;

    private readonly int[] _topIds;
    private readonly int[] _topCounts;

    private StreamWriter _writer;
    private bool _disposed;

    public BehaviourMotifLog(int capacity, int topK)
    {
        _capacity = math.max(capacity, 1);
        _topK     = math.clamp(topK, 1, Slots);
        _last         = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _before       = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _lastBucket   = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _beforeBucket = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _keys         = new NativeArray<ushort>(_capacity * Slots, Allocator.Persistent);
        _counts       = new NativeArray<ushort>(_capacity * Slots, Allocator.Persistent);
        _topIds       = new int[_topK];
        _topCounts    = new int[_topK];
        for (int i = 0; i < _capacity; i++) ClearHistory(i);
        for (int i = 0; i < _keys.Length; i++) _keys[i] = Empty;
    }

    public static int Bucket(byte speechContext)
        => (speechContext & 7) | (((speechContext >> 4) & 1) << 3);

    public static int MotifId(int first, int second, int third, int bucket)
        => ((first * Actions + second) * Actions + third) * ContextBuckets + bucket;

    public static void Decode(int id, out int first, out int second, out int third, out int bucket)
    {
        bucket = id % ContextBuckets;
        int triple = id / ContextBuckets;
        third  = triple % Actions;
        second = (triple / Actions) % Actions;
        first  = triple / (Actions * Actions);
    }

    public void Reset(int index)
    {
        if ((uint)index >= (uint)_capacity) return;
        ClearRow(index);
        ClearHistory(index);
    }

    public void NoteAction(int index, int action, byte speechContext)
    {
        if ((uint)index >= (uint)_capacity || (uint)action >= (uint)Actions) return;

        byte first  = _before[index];
        byte second = _last[index];
        if (first != NoAction) Add(index, MotifId(first, second, action, _beforeBucket[index]));

        _before[index]       = second;
        _beforeBucket[index] = _lastBucket[index];
        _last[index]         = (byte)action;
        _lastBucket[index]   = (byte)Bucket(speechContext);
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)_capacity || (uint)last >= (uint)_capacity) return;
        if (index != last)
        {
            _before[index]       = _before[last];
            _last[index]         = _last[last];
            _beforeBucket[index] = _beforeBucket[last];
            _lastBucket[index]   = _lastBucket[last];
            for (int s = 0; s < Slots; s++)
            {
                _keys[index * Slots + s]   = _keys[last * Slots + s];
                _counts[index * Slots + s] = _counts[last * Slots + s];
            }
        }
        ClearRow(last);
        ClearHistory(last);
    }

    public int CountOf(int index, int motifId)
    {
        int baseIndex = index * Slots;
        for (int s = 0; s < Slots; s++)
            if (_keys[baseIndex + s] == motifId) return _counts[baseIndex + s];
        return 0;
    }

    public int LastAction(int index) => _last[index] == NoAction ? -1 : _last[index];

    private void Add(int index, int id)
    {
        int baseIndex = index * Slots;
        int start = (int)((uint)id * 2654435761u >> 27) & (Slots - 1);
        int weakest = -1;
        int weakestCount = int.MaxValue;
        for (int p = 0; p < Slots; p++)
        {
            int slot = baseIndex + ((start + p) & (Slots - 1));
            ushort key = _keys[slot];
            if (key == id)
            {
                if (_counts[slot] < ushort.MaxValue - 1) _counts[slot]++;
                return;
            }
            if (key == Empty)
            {
                _keys[slot]   = (ushort)id;
                _counts[slot] = 1;
                return;
            }
            if (_counts[slot] >= weakestCount) continue;
            weakestCount = _counts[slot];
            weakest = slot;
        }
        _keys[weakest]   = (ushort)id;
        _counts[weakest] = 1;
    }

    private void ClearRow(int index)
    {
        int baseIndex = index * Slots;
        for (int s = 0; s < Slots; s++)
        {
            _keys[baseIndex + s]   = Empty;
            _counts[baseIndex + s] = 0;
        }
    }

    private void ClearHistory(int index)
    {
        _last[index]         = NoAction;
        _before[index]       = NoAction;
        _lastBucket[index]   = 0;
        _beforeBucket[index] = 0;
    }

    public void Sample(string stamp, double now, List<EntityModel> entities)
    {
        if (_disposed || entities == null) return;
        if (_writer == null && !Open(stamp)) return;

        string time = ((float)now).ToString("G6", Inv);
        var sb = new StringBuilder(256);
        try
        {
            int n = Math.Min(entities.Count, _capacity);
            for (int i = 0; i < n; i++)
            {
                var e = entities[i];
                if (e == null || e.IsDisposed || e.IsDeathPending || e.ManagerIndex != i || e.Colony == null) continue;

                int top = SelectTop(i);
                if (top == 0) continue;

                sb.Clear();
                sb.Append(time).Append(',').Append(e.EntityId.ToString(Inv)).Append(',').Append(e.Colony.ColonyId.ToString(Inv)).Append(',');
                for (int k = 0; k < top; k++)
                {
                    if (k > 0) sb.Append(';');
                    sb.Append(_topIds[k].ToString(Inv)).Append(':').Append(_topCounts[k].ToString(Inv));
                }
                _writer.WriteLine(sb.ToString());
                ClearRow(i);
            }
            _writer.Flush();
        }
        catch (Exception ex)
        {
            RavineLog.Warning($"BehaviourMotifLog: запись остановлена: {ex.Message}");
            Dispose();
        }
    }

    private int SelectTop(int index)
    {
        int baseIndex = index * Slots;
        int top = 0;
        for (int s = 0; s < Slots; s++)
        {
            ushort key = _keys[baseIndex + s];
            if (key == Empty) continue;
            int count = _counts[baseIndex + s];
            if (top == _topK && count <= _topCounts[top - 1]) continue;

            int at = top < _topK ? top++ : top - 1;
            while (at > 0 && _topCounts[at - 1] < count)
            {
                _topIds[at]    = _topIds[at - 1];
                _topCounts[at] = _topCounts[at - 1];
                at--;
            }
            _topIds[at]    = key;
            _topCounts[at] = count;
        }
        return top;
    }

    private bool Open(string stamp)
    {
        try
        {
            string dir = NeuralModelStorage.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _writer = new StreamWriter(Path.Combine(dir, $"behaviour_motifs_{stamp}.csv"), false, new UTF8Encoding(false));
            _writer.WriteLine("time_s,agent_id,colony_id,motifs");
            _writer.Flush();
            return true;
        }
        catch (Exception e)
        {
            RavineLog.Warning($"BehaviourMotifLog: не удалось открыть файл: {e.Message}");
            _writer?.Dispose();
            _writer = null;
            _disposed = true;
            return false;
        }
    }

    public void Dispose()
    {
        if (_last.IsCreated)         _last.Dispose();
        if (_before.IsCreated)       _before.Dispose();
        if (_lastBucket.IsCreated)   _lastBucket.Dispose();
        if (_beforeBucket.IsCreated) _beforeBucket.Dispose();
        if (_keys.IsCreated)         _keys.Dispose();
        if (_counts.IsCreated)       _counts.Dispose();
        _writer?.Dispose();
        _writer = null;
        _disposed = true;
    }
}
