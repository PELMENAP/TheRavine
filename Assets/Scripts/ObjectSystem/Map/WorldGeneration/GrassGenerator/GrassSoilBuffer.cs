using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class GrassSoilBuffer
{
    public const int Side  = 5;
    public const int Cells = Side * Side;

    private static readonly int BufferId  = Shader.PropertyToID("_SoilBuffer");
    private static readonly int OriginId  = Shader.PropertyToID("_SoilOrigin");
    private static readonly int EnabledId = Shader.PropertyToID("_SoilEnabled");

    private static GraphicsBuffer buffer;

    public static void EnsureBound()
    {
        if (buffer != null && buffer.IsValid()) return;

        buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Cells, sizeof(float) * 4);
        var neutral = new float4[Cells];
        for (int i = 0; i < Cells; i++) neutral[i] = new float4(1f, 0f, 0f, 0f);
        buffer.SetData(neutral);

        Shader.SetGlobalBuffer(BufferId, buffer);
        Shader.SetGlobalFloat(EnabledId, 0f);
    }

    public static void Publish(NativeArray<float4> cells, Vector4 origin)
    {
        EnsureBound();
        buffer.SetData(cells);
        Shader.SetGlobalBuffer(BufferId, buffer);
        Shader.SetGlobalVector(OriginId, origin);
        Shader.SetGlobalFloat(EnabledId, 1f);
    }

    public static void Disable() => Shader.SetGlobalFloat(EnabledId, 0f);

    public static void Release()
    {
        Disable();
        buffer?.Release();
        buffer = null;
    }
}
