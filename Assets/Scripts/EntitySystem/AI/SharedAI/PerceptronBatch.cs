using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public sealed unsafe class PerceptronBatch : IDisposable
{
    public readonly int InputSize;
    public readonly int BiasStride;

    private NativeArray<float>      _inputs;
    private NativeArray<float>      _biases;
    private NativeList<ForwardItem>   _items;
    private NativeList<ReservoirItem> _reservoir;
    private int _rows;

    public int Count => _items.Length;
    public int ReservoirCount => _reservoir.Length;
    public NativeArray<float> Inputs => _inputs;

    public PerceptronBatch(int inputSize, int biasStride)
    {
        InputSize  = inputSize;
        BiasStride = biasStride;
        _items     = new NativeList<ForwardItem>(Allocator.Persistent);
        _reservoir = new NativeList<ReservoirItem>(Allocator.Persistent);
    }

    public void ClearReservoir() => _reservoir.Clear();
    public void AddReservoir(float* state, int row) => _reservoir.Add(new ReservoirItem { State = state, InputRow = row });

    public JobHandle ScheduleReservoir(float* w, float* b, int hidden)
        => _reservoir.Length == 0 ? default : new ReservoirJob
        {
            Items     = _reservoir.AsArray(),
            Inputs    = _inputs,
            W         = w,
            B         = b,
            InputSize = InputSize,
            Hidden    = hidden,
        }.Schedule(_reservoir.Length, 8);

    public void SetInput(int row, float[] input)
    {
        EnsureRows(row + 1);
        NativeArray<float>.Copy(input, 0, _inputs, row * InputSize, InputSize);
    }

    public Span<float> BiasRow(int row)
        => new Span<float>((float*)_biases.GetUnsafePtr() + row * BiasStride, BiasStride);

    public void ClearItems() => _items.Clear();
    public void Add(in ForwardItem item) => _items.Add(item);
    public ForwardItem ItemAt(int i) => _items[i];

    public JobHandle Schedule(NativeArray<KernelLayout> kernels, NativeArray<NetWeights> nets, int lstmHidden)
        => new BrainForwardJob
        {
            Items      = _items.AsArray(),
            Inputs     = _inputs,
            Biases     = _biases,
            Layouts    = kernels,
            Nets       = nets,
            InputSize  = InputSize,
            LstmHidden = lstmHidden,
            BiasStride = BiasStride,
        }.Schedule(_items.Length, 1);

    private void EnsureRows(int rows)
    {
        if (rows <= _rows) return;
        int cap = Math.Max(_rows << 1, rows);

        var inputs = new NativeArray<float>(cap * InputSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var biases = new NativeArray<float>(cap * BiasStride, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        if (_inputs.IsCreated)
        {
            NativeArray<float>.Copy(_inputs, inputs, _rows * InputSize);
            _inputs.Dispose();
        }
        if (_biases.IsCreated) _biases.Dispose();

        _inputs = inputs;
        _biases = biases;
        _rows   = cap;
    }

    public void Dispose()
    {
        if (_inputs.IsCreated) _inputs.Dispose();
        if (_biases.IsCreated) _biases.Dispose();
        if (_items.IsCreated)  _items.Dispose();
        if (_reservoir.IsCreated) _reservoir.Dispose();
        _rows = 0;
    }
}