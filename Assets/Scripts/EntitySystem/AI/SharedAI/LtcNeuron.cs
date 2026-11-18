using System.Runtime.CompilerServices;
using Unity.Mathematics;

public enum LtcMode : byte
{
    Legacy  = 0,
    Bounded = 1,
}

public static class LtcNeuron
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Softplus(float x) => x > 20f ? x : math.log(1f + math.exp(x));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tau(float preT, float tauMin, float tauMax)
        => math.min(math.max(Softplus(preT), tauMin), tauMax);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Forward(float h, float f, float tau, float dt, bool bounded, out float stored)
    {
        if (bounded)
        {
            float z = dt / (tau + dt);
            stored = z;
            return (1f - z) * h + z * f;
        }

        float a = 1f + dt / tau;
        stored = a;
        return (h + dt * f) / a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Backward(float dH, float hBefore, float f, float tau, float stored, float dt,
        bool bounded, float tauMin, float tauMax, out float dPreF, out float dPreT, out float dHBefore)
    {
        if (bounded)
        {
            float z    = stored;
            float dTau = dH * (-z / (tau + dt)) * (f - hBefore);
            bool open  = tau > tauMin && tau < tauMax;

            dPreF   = dH * z * (1f - f * f);
            dPreT   = open ? dTau * (1f - math.exp(-tau)) : 0f;
            dHBefore = dH * (1f - z);
            return;
        }

        float taun = math.max(tau, DelayedPerceptron.TauEpsilon);
        float An   = stored;
        float hNew = (hBefore + dt * f) / An;
        float dT   = dH * hNew * dt / (An * taun * taun);

        dPreF    = dH * (dt / An) * (1f - f * f);
        dPreT    = dT * (1f - math.exp(-taun));
        dHBefore = dH / An;
    }
}
