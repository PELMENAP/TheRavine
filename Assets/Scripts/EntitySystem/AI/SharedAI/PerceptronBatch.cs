using System;
using Unity.Collections;

public sealed class PerceptronBatch : IDisposable
{
    public readonly PerceptronLayout Layout;

    private NativeArray<float> _contexts;
    private NativeArray<float> _weights;
    private NativeArray<float> _biases;
    private NativeArray<int>   _agents;
    private NativeArray<int>   _slots;
    private NativeArray<int>   _stamps;

    private PerceptronContext[] _bound;
    private int _count;
    private int _capacity;

    public int Count => _count;
    public NativeArray<float> Contexts => _contexts;
    public NativeArray<float> Weights  => _weights;
    public NativeArray<float> Biases   => _biases;
    public NativeArray<int>   Slots    => _slots;
    public NativeArray<int>   Stamps   => _stamps;

    public PerceptronBatch(PerceptronLayout layout, int capacity)
    {
        Layout    = layout;
        _capacity = capacity;
        _bound    = new PerceptronContext[capacity];

        _contexts = new NativeArray<float>(capacity * layout.Stride, Allocator.Persistent,
                                           NativeArrayOptions.ClearMemory);
        _weights  = new NativeArray<float>(layout.WeightTotal, Allocator.Persistent,
                                           NativeArrayOptions.UninitializedMemory);
        _biases   = new NativeArray<float>(layout.BiasTotal, Allocator.Persistent,
                                           NativeArrayOptions.UninitializedMemory);
        _agents   = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _slots    = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _stamps   = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    public void BeginBatch() => _count = 0;

    public bool Add(PerceptronContext ctx, int agentIndex)
    {
        if (_count >= _capacity) return false;
        if (!ReferenceEquals(ctx.Layout, Layout)) return false;

        _bound[_count]  = ctx;
        _agents[_count] = agentIndex;
        _slots[_count]  = ctx.BpttPtr;
        _stamps[_count] = ctx.NextForwardStamp();
        _count++;
        return true;
    }

    public void UploadWeights(float[] wt, float[] bt)
    {
        NativeArray<float>.Copy(wt, _weights, wt.Length);
        NativeArray<float>.Copy(bt, _biases, bt.Length);
    }

    public void UploadContexts()
    {
        int stride = Layout.Stride;
        for (int i = 0; i < _count; i++)
            NativeArray<float>.Copy(_bound[i].RawBuffer, 0, _contexts, i * stride, stride);
    }

    public void DownloadContexts()
    {
        int stride = Layout.Stride;
        for (int i = 0; i < _count; i++)
            NativeArray<float>.Copy(_contexts, i * stride, _bound[i].RawBuffer, 0, stride);
    }

    public PerceptronContext ContextAt(int i) => _bound[i];
    public int AgentAt(int i) => _agents[i];

    public void Dispose()
    {
        if (_contexts.IsCreated) _contexts.Dispose();
        if (_weights.IsCreated)  _weights.Dispose();
        if (_biases.IsCreated)   _biases.Dispose();
        if (_agents.IsCreated)   _agents.Dispose();
        if (_slots.IsCreated)    _slots.Dispose();
        if (_stamps.IsCreated)   _stamps.Dispose();
        _bound = null;
    }
}