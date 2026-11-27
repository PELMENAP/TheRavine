using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public class LtcNeuronTests
{
    private const int Neurons = 3;
    private const int Inputs  = 2;
    private const int Steps   = 4;
    private const float Dt    = 0.3f;

    private sealed class Net
    {
        public float[] WF = new float[Neurons * Inputs];
        public float[] WT = new float[Neurons * Inputs];
        public float[] BF = new float[Neurons];
        public float[] BT = new float[Neurons];
        public float[] H0 = new float[Neurons];
        public float[][] X = new float[Steps][];
        public float[][] C = new float[Steps][];

        public Net(int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)(rng.NextDouble() * 2.0 - 1.0);
            for (int i = 0; i < WF.Length; i++) { WF[i] = R(); WT[i] = R(); }
            for (int n = 0; n < Neurons; n++) { BF[n] = 0.3f * R(); BT[n] = 0.5f * R(); H0[n] = 0.5f * R(); }
            for (int t = 0; t < Steps; t++)
            {
                X[t] = new[] { R(), R() };
                C[t] = new[] { R(), R(), R() };
            }
        }
    }

    private static float Loss(Net net, bool bounded, float tauMin, float tauMax)
    {
        var h = (float[])net.H0.Clone();
        float loss = 0f;
        for (int t = 0; t < Steps; t++)
        {
            for (int n = 0; n < Neurons; n++)
            {
                float preF = net.BF[n], preT = net.BT[n];
                for (int i = 0; i < Inputs; i++)
                {
                    preF += net.WF[n * Inputs + i] * net.X[t][i];
                    preT += net.WT[n * Inputs + i] * net.X[t][i];
                }
                float f   = math.tanh(preF);
                float tau = LtcNeuron.Tau(preT, tauMin, tauMax);
                h[n] = LtcNeuron.Forward(h[n], f, tau, Dt, bounded, out _);
                loss += net.C[t][n] * h[n];
            }
        }
        return loss;
    }

    private static void Analytic(Net net, bool bounded, float tauMin, float tauMax,
        float[] gWF, float[] gWT, float[] gBF, float[] gBT, float[] gH0)
    {
        var h = new float[Steps + 1][];
        var f = new float[Steps][];
        var tau = new float[Steps][];
        var stored = new float[Steps][];
        h[0] = (float[])net.H0.Clone();
        for (int t = 0; t < Steps; t++)
        {
            h[t + 1] = new float[Neurons];
            f[t] = new float[Neurons];
            tau[t] = new float[Neurons];
            stored[t] = new float[Neurons];
            for (int n = 0; n < Neurons; n++)
            {
                float preF = net.BF[n], preT = net.BT[n];
                for (int i = 0; i < Inputs; i++)
                {
                    preF += net.WF[n * Inputs + i] * net.X[t][i];
                    preT += net.WT[n * Inputs + i] * net.X[t][i];
                }
                f[t][n] = math.tanh(preF);
                tau[t][n] = LtcNeuron.Tau(preT, tauMin, tauMax);
                h[t + 1][n] = LtcNeuron.Forward(h[t][n], f[t][n], tau[t][n], Dt, bounded, out stored[t][n]);
            }
        }

        var carry = new float[Neurons];
        for (int t = Steps - 1; t >= 0; t--)
        {
            var next = new float[Neurons];
            for (int n = 0; n < Neurons; n++)
            {
                float dH = net.C[t][n] + carry[n];
                LtcNeuron.Backward(dH, h[t][n], f[t][n], tau[t][n], stored[t][n], Dt, bounded, tauMin, tauMax,
                    out float dPreF, out float dPreT, out float dHBefore);
                for (int i = 0; i < Inputs; i++)
                {
                    gWF[n * Inputs + i] += dPreF * net.X[t][i];
                    gWT[n * Inputs + i] += dPreT * net.X[t][i];
                }
                gBF[n] += dPreF;
                gBT[n] += dPreT;
                next[n] = dHBefore;
            }
            carry = next;
        }
        for (int n = 0; n < Neurons; n++) gH0[n] = carry[n];
    }

    private static void CheckParameter(Net net, float[] values, float analytic, int index, bool bounded, string what)
    {
        const float eps = 1e-2f;
        float keep = values[index];
        values[index] = keep + eps;
        float up = Loss(net, bounded, DelayedPerceptron.TauEpsilon, float.MaxValue);
        values[index] = keep - eps;
        float down = Loss(net, bounded, DelayedPerceptron.TauEpsilon, float.MaxValue);
        values[index] = keep;

        float numeric = (up - down) / (2f * eps);
        Assert.AreEqual(numeric, analytic, 3e-3f + 2e-2f * Mathf.Abs(numeric), $"{what}[{index}] {(bounded ? "bounded" : "legacy")}");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BackwardMatchesFiniteDifferences(bool bounded)
    {
        for (int seed = 1; seed <= 5; seed++)
        {
            var net = new Net(seed);
            var gWF = new float[Neurons * Inputs];
            var gWT = new float[Neurons * Inputs];
            var gBF = new float[Neurons];
            var gBT = new float[Neurons];
            var gH0 = new float[Neurons];
            Analytic(net, bounded, DelayedPerceptron.TauEpsilon, float.MaxValue, gWF, gWT, gBF, gBT, gH0);

            for (int i = 0; i < gWF.Length; i++)
            {
                CheckParameter(net, net.WF, gWF[i], i, bounded, "wF");
                CheckParameter(net, net.WT, gWT[i], i, bounded, "wT");
            }
            for (int n = 0; n < Neurons; n++)
            {
                CheckParameter(net, net.BF, gBF[n], n, bounded, "bF");
                CheckParameter(net, net.BT, gBT[n], n, bounded, "bT");
                CheckParameter(net, net.H0, gH0[n], n, bounded, "h0");
            }
        }
    }

    [Test]
    public void LegacyFormulaIsBitIdenticalToTheOriginalKernel()
    {
        var rng = new System.Random(11);
        for (int i = 0; i < 2000; i++)
        {
            float preT = (float)(rng.NextDouble() * 12.0 - 6.0);
            float f    = (float)(rng.NextDouble() * 2.0 - 1.0);
            float h    = (float)(rng.NextDouble() * 6.0 - 3.0);
            float dt   = (float)(rng.NextDouble() * 1.5 + 0.01);

            float tauOld = math.max(NeuralKernels.Softplus(preT), DelayedPerceptron.TauEpsilon);
            float tau    = LtcNeuron.Tau(preT, DelayedPerceptron.TauEpsilon, float.MaxValue);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(tauOld), BitConverter.SingleToInt32Bits(tau));

            float A     = 1f + dt / tauOld;
            float hOld  = (h + dt * f) / A;
            float hNew  = LtcNeuron.Forward(h, f, tau, dt, false, out float stored);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(hOld), BitConverter.SingleToInt32Bits(hNew));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(A), BitConverter.SingleToInt32Bits(stored));

            float dH    = (float)(rng.NextDouble() * 2.0 - 1.0);
            LegacyBackwardReference(dH, h, f, tauOld, A, dt, out float dPreFOld, out float dPreTOld, out float dHBeforeOld);
            LegacyBackward(dH, h, f, tau, stored, dt,
                out float dPreF, out float dPreT, out float dHBefore);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(dPreFOld), BitConverter.SingleToInt32Bits(dPreF));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(dPreTOld), BitConverter.SingleToInt32Bits(dPreT));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(dHBeforeOld), BitConverter.SingleToInt32Bits(dHBefore));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LegacyBackwardReference(float dH, float hBefore, float f, float tau, float a, float dt,
        out float dPreF, out float dPreT, out float dHBefore)
    {
        float taun = math.max(tau, DelayedPerceptron.TauEpsilon);
        dPreF = dH * (dt / a) * (1f - f * f);
        float hNew = (hBefore + dt * f) / a;
        float dTau = dH * hNew * dt / (a * taun * taun);
        dPreT = dTau * (1f - math.exp(-taun));
        dHBefore = dH / a;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LegacyBackward(float dH, float hBefore, float f, float tau, float a, float dt,
        out float dPreF, out float dPreT, out float dHBefore)
        => LtcNeuron.Backward(dH, hBefore, f, tau, a, dt, false, DelayedPerceptron.TauEpsilon, float.MaxValue,
            out dPreF, out dPreT, out dHBefore);

    [Test]
    public void BoundedStateStaysInUnitInterval()
    {
        var rng = new System.Random(5);
        for (int run = 0; run < 50; run++)
        {
            float h = (float)(rng.NextDouble() * 2.0 - 1.0);
            for (int step = 0; step < 400; step++)
            {
                float preF = (float)(rng.NextDouble() * 20.0 - 10.0);
                float preT = (float)(rng.NextDouble() * 20.0 - 10.0);
                float dt   = (float)(rng.NextDouble() * 3.0 + 0.001);
                float tau  = LtcNeuron.Tau(preT, DelayedPerceptron.TauEpsilon, float.MaxValue);
                h = LtcNeuron.Forward(h, math.tanh(preF), tau, dt, true, out _);
                Assert.LessOrEqual(Mathf.Abs(h), 1f + 1e-5f);
            }
        }
    }

    [Test]
    public void LegacyStateIsNotBoundedByOne()
    {
        float h = 0f;
        for (int step = 0; step < 200; step++)
            h = LtcNeuron.Forward(h, 1f, 5f, 0.5f, false, out _);
        Assert.Greater(h, 1.5f);
    }

    [Test]
    public void ClampedTauHasNoGradientInBoundedMode()
    {
        LtcNeuron.Backward(1f, 0.2f, 0.7f, 0.5f, 0.4f, 0.3f, true, 0.5f, 2f, out _, out float clampedMin, out _);
        LtcNeuron.Backward(1f, 0.2f, 0.7f, 2f, 0.4f, 0.3f, true, 0.5f, 2f, out _, out float clampedMax, out _);
        LtcNeuron.Backward(1f, 0.2f, 0.7f, 1f, 0.4f, 0.3f, true, 0.5f, 2f, out _, out float open, out _);
        Assert.AreEqual(0f, clampedMin);
        Assert.AreEqual(0f, clampedMax);
        Assert.AreNotEqual(0f, open);
    }

    [Test]
    public void ArmSelectsTheModeAndBoundedHiddenStateStaysBounded()
    {
        var rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(rules);
        SimulationClock.Reset();
        ContextSlabs.Reserve(8);
        var brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, 32);
        var ctx = brain.CreateContext();
        try
        {
            brain.SetArm(ColonyArm.BoundedLtc, false);
            var x = new float[InputVectorizer.VectorSize];
            float t = 0f;
            for (int i = 0; i < 60; i++)
            {
                for (int k = 0; k < x.Length; k++) x[k] = 0.5f * Mathf.Sin(i * 0.7f + k * 0.31f);
                t += 1000f;
                brain.BeginDecisionBatch();
                int slot = brain.EnqueueDecision(ctx, x, t, 0.05f);
                brain.RunDecisions();
                brain.CompletePending();
                if (brain.TryGetDecision(slot, out var decision))
                    brain.CompleteDecision(in decision, (i & 1) == 0 ? 1f : -1f, ctx, t + 1f, EntityCommandStatus.Completed);
                brain.ApplyPendingGradients();
            }

            long saturated = 0, total = 0;
            ctx.CoordMLP.CountSaturated(1.0001f, ref saturated, ref total);
            for (int g = 0; g < ctx.ExecMLPs.Length; g++) ctx.ExecMLPs[g].CountSaturated(1.0001f, ref saturated, ref total);
            Assert.Greater(total, 0);
            Assert.AreEqual(0, saturated);
        }
        finally
        {
            brain.CompletePending();
            ctx.Dispose();
            brain.Dispose();
            ContextSlabs.DisposeAll();
            UnityEngine.Object.DestroyImmediate(rules);
            SimulationRules.Bind(null);
            SimulationClock.Reset();
        }
    }
}
