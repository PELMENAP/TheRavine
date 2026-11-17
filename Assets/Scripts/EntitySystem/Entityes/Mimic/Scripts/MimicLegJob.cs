using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public unsafe struct MimicWork
{
    public LegParams* Legs;
    public float3* Handles;
    public float3* Offsets;
    public float3* Vertices;
    public float3* Normals;
    public float3 Position;
    public float Dt;
    public float HalfWidth;
    public int LegCount;
    public int Samples;
    public int Ring;
    public int Resolution;
    public int VertexCount;
    public int ActiveLegs;
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public unsafe struct MimicLegJob : IJobParallelFor
{
    public NativeArray<MimicWork> Work;

    public void Execute(int index)
    {
        var w = Work[index];
        int vIdx = 0;
        int active = 0;

        bool mesh = w.Samples >= 3;
        int segments = w.Samples - 1;
        int last = w.Resolution - 1;
        float invLast = 1f / last;
        float invHalfWidth = w.HalfWidth > 1e-6f ? 1f / w.HalfWidth : 0f;
        float sinTheta = 0f;
        float cosTheta = 1f;
        if (mesh) math.sincos(2f * math.PI / w.Ring, out sinTheta, out cosTheta);
        float oneMinusCos = 1f - cosTheta;
        float3 up = new float3(0f, 1f, 0f);

        for (int l = 0; l < w.LegCount; l++)
        {
            LegParams* leg = w.Legs + l;
            if (leg->State == LegState.Disabled) continue;
            active++;

            float3* h = w.Handles + l * LegStore.HandleCount;
            float3* o = w.Offsets + l * LegStore.HandleCount;

            leg->Progression = math.lerp(leg->Progression, leg->GrowTarget, math.saturate(leg->GrowCoef * w.Dt));
            UpdateHandles(leg, h, o, w.Position, w.Dt);

            if (!mesh) continue;

            float progression = leg->Progression;
            bool grown = progression >= 0.0001f;
            float3 p1 = Bezier(h, 0f);
            float3 dir = up;

            for (int k = 0; k < segments; k++)
            {
                float t = grown ? ((k + 1) * last / segments) * invLast * progression : 0f;
                float3 p2 = Bezier(h, t);

                float3 d = p2 - p1;
                float dl2 = math.lengthsq(d);
                dir = dl2 > 1e-10f ? d * math.rsqrt(dl2) : float3.zero;
                if (math.lengthsq(dir) < 0.001f) dir = up;

                float3 side = math.cross(dir, up);
                float sl2 = math.lengthsq(side);
                float3 perp = sl2 > 1e-10f ? side * (math.rsqrt(sl2) * w.HalfWidth) : float3.zero;
                if (math.lengthsq(perp) < 0.001f) perp = new float3(w.HalfWidth, 0f, 0f);

                float3 center = p1 - w.Position;
                for (int j = 0; j < w.Ring; j++)
                {
                    w.Vertices[vIdx] = center + perp;
                    w.Normals[vIdx] = perp * invHalfWidth;
                    vIdx++;

                    float3 cross = math.cross(dir, perp);
                    float dot = math.dot(dir, perp);
                    perp = perp * cosTheta + cross * sinTheta + dir * (dot * oneMinusCos);
                }
                p1 = p2;
            }

            w.Vertices[vIdx] = p1 - w.Position;
            w.Normals[vIdx] = dir;
            vIdx++;
        }

        w.VertexCount = vIdx;
        w.ActiveLegs = active;
        Work[index] = w;
    }

    private static void UpdateHandles(LegParams* leg, float3* h, float3* o, float3 rootPos, float dt)
    {
        h[0] = rootPos;
        h[6] = leg->Foot + new float3(0f, 0.05f, 0f);

        h[2] = math.lerp(h[0], h[6], 0.4f);
        h[2].y = h[0].y + leg->LegHeight;

        h[1] = math.lerp(h[0], h[2], 0.5f);
        h[3] = math.lerp(h[2], h[6], 0.25f);
        h[4] = math.lerp(h[2], h[6], 0.5f);
        h[5] = math.lerp(h[2], h[6], 0.75f);

        RotateHandleOffsets(leg, h, o, dt);

        h[1] += o[0];
        h[2] += o[1];
        h[3] += o[2];
        h[4] += o[3] * 0.5f;
        h[5] += o[4] * 0.25f;
    }

    private static void RotateHandleOffsets(LegParams* leg, float3* h, float3* o, float dt)
    {
        float oscillation = leg->OscillationProgress + dt * leg->OscillationSpeed;
        if (oscillation >= 360f) oscillation -= 360f;
        leg->OscillationProgress = oscillation;

        float angle = leg->RotationSpeed * math.sin(math.radians(oscillation));
        math.sincos(math.radians(angle), out float sinTheta, out float cosTheta);
        float oneMinusCos = 1f - cosTheta;

        for (int i = 1; i < 6; i++)
        {
            float3 axis = h[i + 1] - h[i - 1];
            float l2 = math.lengthsq(axis);
            if (l2 <= 0.0001f) continue;

            axis *= math.rsqrt(l2);
            float3 v = o[i - 1];
            o[i - 1] = v * cosTheta + math.cross(axis, v) * sinTheta + axis * (math.dot(axis, v) * oneMinusCos);
        }
    }

    private static float3 Bezier(float3* h, float t)
    {
        float u = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float u4 = u3 * u;
        float u5 = u4 * u;
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        float t5 = t4 * t;

        return h[0] * (u5 * u) +
               h[1] * (6f * t * u5) +
               h[2] * (15f * t2 * u4) +
               h[3] * (20f * t3 * u3) +
               h[4] * (15f * t4 * u2) +
               h[5] * (6f * t5 * u) +
               h[6] * (t5 * t);
    }
}
