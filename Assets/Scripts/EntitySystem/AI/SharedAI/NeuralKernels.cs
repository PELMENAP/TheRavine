using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public static unsafe class NeuralKernels
{
    public const int MaxLstmHidden = 128;

    public static float Softplus(float x) => x > 20f ? x : math.log(1f + math.exp(x));

    private static float FastSigmoid(float x) => 0.5f * (x / (1f + math.abs(x))) + 0.5f;
    private static float FastTanh(float x)    => x / (1f + math.abs(x));

    public static void Softmax(float* vals, float* buf, int count, float temp)
    {
        float max = vals[0];
        for (int i = 1; i < count; i++) max = math.max(max, vals[i]);

        float invT = 1f / temp;
        float sum  = 0f;
        for (int i = 0; i < count; i++)
        {
            buf[i] = math.exp((vals[i] - max) * invT);
            sum += buf[i];
        }

        float inv = 1f / sum;
        for (int i = 0; i < count; i++) vals[i] = buf[i] * inv;
    }

    public static void ReservoirStep(float* w, float* b, float* x, float* state, int inSize, int hidden)
    {
        float* H = state;
        float* C = state + hidden;
        int cols = inSize + hidden;
        int rows = hidden << 2;

        float* gates = stackalloc float[MaxLstmHidden << 2];

        for (int r = 0; r < rows; r++)
        {
            float* row = w + r * cols;
            float sum = b[r];
            for (int j = 0; j < inSize; j++) sum += row[j] * x[j];
            float* rh = row + inSize;
            for (int j = 0; j < hidden; j++) sum += rh[j] * H[j];
            gates[r] = sum;
        }

        for (int j = 0; j < hidden; j++)
        {
            float f  = FastSigmoid(gates[j]);
            float i  = FastSigmoid(gates[j + hidden]);
            float o  = FastSigmoid(gates[j + 2 * hidden]);
            float ct = FastTanh(gates[j + 3 * hidden]);

            C[j] = f * C[j] + i * ct;
            H[j] = o * FastTanh(C[j]);
        }
    }

    public static void Combine(float* x, float* h, int inSize, int hidden, float* combined)
    {
        UnsafeUtility.MemCpy(combined, x, (long)inSize * sizeof(float));
        UnsafeUtility.MemCpy(combined + inSize, h, (long)hidden * sizeof(float));
    }

    private static void Layer(float* inp, float* h, float* act, float* w, float* b,
        int inputs, int neurons, bool residual, float dt, float* fOut, float* tauOut, float* aOut)
    {
        int rowLen = inputs << 1;
        for (int n = 0; n < neurons; n++)
        {
            float* row = w + n * rowLen;
            float preF = b[n << 1];
            float preT = b[(n << 1) + 1];

            for (int i = 0; i < inputs; i++)
            {
                float a = inp[i];
                preF += row[i << 1] * a;
                preT += row[(i << 1) + 1] * a;
            }

            float f   = math.tanh(preF);
            float tau = math.max(Softplus(preT), DelayedPerceptron.TauEpsilon);
            float A   = 1f + dt / tau;

            if (fOut != null)
            {
                fOut[n]   = f;
                tauOut[n] = tau;
                aOut[n]   = A;
            }

            float hv = (h[n] + dt * f) / A;
            h[n]   = hv;
            act[n] = residual ? hv + inp[n] : hv;
        }
    }

    public static void Forward(KernelLayout* lay, float* w, float* b, float* ctx,
        int slot, float dt, float temp, float* logitBias)
    {
        int L  = lay->L;
        int hs = lay->HiddenSum;
        float* slotBase = ctx + lay->BpttBase + slot * lay->SlotStride;

        for (int l = 0; l < L; l++)
        {
            int inputs  = lay->Sizes[l];
            int neurons = lay->Sizes[l + 1];

            float* inp = ctx + lay->ActOffset[l];
            float* h   = ctx + lay->HidOffset[l];
            float* act = ctx + lay->ActOffset[l + 1];

            float* sPrev = slotBase + lay->SlotActOffset[l];
            float* sH    = slotBase + lay->SlotHidOffset[l];

            UnsafeUtility.MemCpy(sPrev, inp, (long)inputs * sizeof(float));
            UnsafeUtility.MemCpy(sH, h, (long)neurons * sizeof(float));

            Layer(inp, h, act,
                w + lay->WeightRowOffset[l], b + lay->BiasRowOffset[l],
                inputs, neurons, lay->Residual[l] != 0, dt,
                sH + hs, sH + 2 * hs, sH + 3 * hs);
        }

        float* outAct = ctx + lay->ActOffset[L];
        int ac = lay->ActionCount;
        float* softBuf = ctx + lay->SoftmaxOffset;

        if (logitBias != null)
        {
            float* biased = ctx + lay->LogitScratchOffset;
            for (int i = 0; i < ac; i++) biased[i] = outAct[i] + logitBias[i];
            Softmax(biased, softBuf, ac, temp);
            UnsafeUtility.MemCpy(ctx + lay->BiasedProbsOffset, biased, (long)ac * sizeof(float));
        }

        Softmax(outAct, softBuf, ac, temp);
    }

    public static void EvaluatePolicy(KernelLayout* lay, float* w, float* b, float* ctx, int t,
        float dt, float temp, float* evalAct, float* evalHid, float* softBuf, float* outProbs)
    {
        int L = lay->L;
        float* slotBase = ctx + lay->BpttBase + t * lay->SlotStride;

        UnsafeUtility.MemCpy(evalAct, slotBase + lay->SlotActOffset[0], (long)lay->InputSize * sizeof(float));

        for (int l = 0; l < L; l++)
        {
            int inputs  = lay->Sizes[l];
            int neurons = lay->Sizes[l + 1];

            float* inp = evalAct + lay->ActOffset[l];
            float* h   = evalHid + lay->NeuronOffset[l];
            float* act = evalAct + lay->ActOffset[l + 1];

            UnsafeUtility.MemCpy(h, slotBase + lay->SlotHidOffset[l], (long)neurons * sizeof(float));

            Layer(inp, h, act,
                w + lay->WeightRowOffset[l], b + lay->BiasRowOffset[l],
                inputs, neurons, lay->Residual[l] != 0, dt, null, null, null);
        }

        int ac = lay->ActionCount;
        float* outAct = evalAct + lay->ActOffset[L];
        Softmax(outAct, softBuf, ac, temp);
        UnsafeUtility.MemCpy(outProbs, outAct, (long)ac * sizeof(float));
    }

    public static int Train(TrainTicket* tk, KernelLayout* lay, float* w, float* b,
        float* grad, byte* gTouched, float* work, float clipEps, out float norm, out int nonFinite)
    {
        norm = 0f;
        nonFinite = 0;

        int L       = lay->L;
        int hs      = lay->HiddenSum;
        int ac      = lay->ActionCount;
        int outSize = lay->OutputSize;

        float* deltaA      = work;
        float* deltaB      = work + hs;
        float* outErr      = work + 2 * hs;
        float* evalAct     = outErr + outSize;
        float* evalHid     = evalAct + lay->ActTotal;
        float* evalSoftmax = evalHid + hs;
        float* evalProbs   = evalSoftmax + ac;

        float dt      = tk->Dt;
        float invN    = 1f / ac;
        float entReg  = tk->EntropyRegularization;
        float invTemp = 1f / math.max(tk->Temperature, 1e-3f);
        int   pred    = tk->Pred;

        float* probs;
        if (tk->UseStored != 0) probs = tk->Probs;
        else
        {
            EvaluatePolicy(lay, w, b, tk->Ctx, tk->Slot, dt, tk->Temperature,
                evalAct, evalHid, evalSoftmax, evalProbs);
            probs = evalProbs;
        }

        float eps   = tk->Epsilon;
        float pPure = probs[pred];
        float pMix  = (1f - eps) * pPure + eps * invN;

        float ratio = math.exp(math.log(math.max(pMix, 1e-8f)) - tk->LogProbability);
        if (!math.isfinite(ratio)) ratio = 1f;
        float mixScale = pMix > 1e-8f ? (1f - eps) * pPure / pMix : 1f;

        float adv = tk->Advantage;
        bool clipped = (adv > 0f && ratio > 1f + clipEps) || (adv < 0f && ratio < 1f - clipEps);
        float gate = clipped ? 0f : ratio * adv;

        if (clipped && entReg <= 0f) return 0;

        float policyGate = gate * mixScale;

        UnsafeUtility.MemClear(outErr, (long)outSize * sizeof(float));
        for (int i = 0; i < ac; i++)
        {
            float oneHot = i == pred ? 1f : 0f;
            float p = probs[i];
            outErr[i] = policyGate * (oneHot - p) * invTemp + entReg * (invN - p);
        }

        const float durVar = DelayedPerceptron.DurationNoiseSigma * DelayedPerceptron.DurationNoiseSigma;
        const float auxVar = DelayedPerceptron.AuxNoiseSigma      * DelayedPerceptron.AuxNoiseSigma;

        outErr[lay->DurationIndex] = gate * tk->DurationNoise / durVar;
        int aux = lay->AuxOutputs;
        for (int i = 0; i < aux; i++)
            outErr[lay->HeadingIndex + i] = gate * tk->AuxNoise[i] / auxVar;

        UnsafeUtility.MemClear(deltaA, (long)hs * sizeof(float));
        UnsafeUtility.MemClear(deltaB, (long)hs * sizeof(float));

        float* wd = deltaA;
        float* td = deltaB;

        int history = lay->HistoryDepth;
        int steps   = tk->Steps;
        int wTotal  = lay->WeightTotal;

        for (int step = 0; step < steps; step++)
        {
            int t = tk->Slot - step;
            if (t < 0) t += history;

            float* slotBase = tk->Ctx + lay->BpttBase + t * lay->SlotStride;

            if (step == 0)
            {
                float* last = wd + lay->NeuronOffset[L - 1];
                for (int i = 0; i < outSize; i++) last[i] += outErr[i];
            }

            for (int l = L - 1; l >= 0; l--)
            {
                int neurons = lay->Sizes[l + 1];
                int inputs  = lay->Sizes[l];
                int rowLen  = inputs << 1;

                float* prevActs = slotBase + lay->SlotActOffset[l];
                float* hBef     = slotBase + lay->SlotHidOffset[l];
                float* fArr     = hBef + hs;
                float* tauArr   = hBef + 2 * hs;
                float* aArr     = hBef + 3 * hs;

                float* wDH    = wd + lay->NeuronOffset[l];
                float* tempDH = td + lay->NeuronOffset[l];

                bool   hasPrev = l > 0;
                float* prevWDH = hasPrev ? wd + lay->NeuronOffset[l - 1] : null;

                UnsafeUtility.MemClear(tempDH, (long)neurons * sizeof(float));

                if (lay->Residual[l] != 0 && hasPrev)
                    for (int i = 0; i < neurons; i++) prevWDH[i] += wDH[i];

                float* wl      = w + lay->WeightRowOffset[l];
                float* gl      = grad + lay->WeightRowOffset[l];
                float* gb      = grad + wTotal + lay->BiasRowOffset[l];
                byte*  touched = gTouched + lay->NeuronOffset[l];

                for (int n = 0; n < neurons; n++)
                {
                    float dH = wDH[n];
                    if (dH == 0f) continue;
                    if (!math.isfinite(dH)) { wDH[n] = 0f; nonFinite++; continue; }

                    float fn   = fArr[n];
                    float taun = math.max(tauArr[n], DelayedPerceptron.TauEpsilon);
                    float An   = aArr[n];

                    float dPreF = dH * (dt / An) * (1f - fn * fn);
                    float hNew  = (hBef[n] + dt * fn) / An;
                    float dTau  = dH * hNew * dt / (An * taun * taun);
                    float dPreT = dTau * (1f - math.exp(-taun));

                    if (!math.isfinite(dPreF) || !math.isfinite(dPreT))
                    {
                        tempDH[n] = 0f;
                        nonFinite++;
                        continue;
                    }

                    tempDH[n] = dH / An;

                    float* wRow = wl + n * rowLen;
                    float* gRow = gl + n * rowLen;
                    touched[n] = 1;

                    if (hasPrev)
                    {
                        for (int i = 0; i < inputs; i++)
                        {
                            int k = i << 1;
                            float a = prevActs[i];

                            float back = dPreF * wRow[k] + dPreT * wRow[k + 1];
                            if (math.isfinite(back)) prevWDH[i] += back;
                            else nonFinite++;

                            gRow[k]     += dPreF * a;
                            gRow[k + 1] += dPreT * a;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < inputs; i++)
                        {
                            int k = i << 1;
                            float a = prevActs[i];
                            gRow[k]     += dPreF * a;
                            gRow[k + 1] += dPreT * a;
                        }
                    }

                    gb[n << 1]       += dPreF;
                    gb[(n << 1) + 1] += dPreT;
                }
            }

            float* swap = wd;
            wd = td;
            td = swap;
        }

        norm = (float)math.sqrt(SquaredNorm(lay, grad, gTouched));
        if (!math.isfinite(norm))
        {
            nonFinite++;
            return 2;
        }
        return 1;
    }

    public static double SquaredNorm(KernelLayout* lay, float* grad, byte* touched)
    {
        double sq = 0d;
        int wTotal = lay->WeightTotal;

        for (int l = 0; l < lay->L; l++)
        {
            int neurons = lay->Sizes[l + 1];
            int rowLen  = lay->Sizes[l] << 1;
            int nOff    = lay->NeuronOffset[l];

            for (int n = 0; n < neurons; n++)
            {
                if (touched[nOff + n] == 0) continue;

                float* row = grad + lay->WeightRowOffset[l] + n * rowLen;
                for (int k = 0; k < rowLen; k++) sq += (double)row[k] * row[k];

                float* bias = grad + wTotal + lay->BiasRowOffset[l] + (n << 1);
                sq += (double)bias[0] * bias[0] + (double)bias[1] * bias[1];
            }
        }
        return sq;
    }

    public static void AccumulateAndClear(KernelLayout* lay, float* grad, byte* gTouched,
        float* acc, byte* aTouched, float scale)
    {
        int wTotal = lay->WeightTotal;

        for (int l = 0; l < lay->L; l++)
        {
            int neurons = lay->Sizes[l + 1];
            int rowLen  = lay->Sizes[l] << 1;
            int nOff    = lay->NeuronOffset[l];

            for (int n = 0; n < neurons; n++)
            {
                int id = nOff + n;
                if (gTouched[id] == 0) continue;
                gTouched[id] = 0;
                aTouched[id] = 1;

                int wi = lay->WeightRowOffset[l] + n * rowLen;
                for (int k = 0; k < rowLen; k++)
                {
                    acc[wi + k] += grad[wi + k] * scale;
                    grad[wi + k] = 0f;
                }

                int bi = wTotal + lay->BiasRowOffset[l] + (n << 1);
                acc[bi]      += grad[bi] * scale;
                acc[bi + 1]  += grad[bi + 1] * scale;
                grad[bi]      = 0f;
                grad[bi + 1]  = 0f;
            }
        }
    }

    public static void ClearGrad(KernelLayout* lay, float* grad, byte* gTouched)
    {
        int wTotal = lay->WeightTotal;

        for (int l = 0; l < lay->L; l++)
        {
            int neurons = lay->Sizes[l + 1];
            int rowLen  = lay->Sizes[l] << 1;
            int nOff    = lay->NeuronOffset[l];

            for (int n = 0; n < neurons; n++)
            {
                int id = nOff + n;
                if (gTouched[id] == 0) continue;
                gTouched[id] = 0;

                UnsafeUtility.MemClear(grad + lay->WeightRowOffset[l] + n * rowLen, (long)rowLen * sizeof(float));
                int bi = wTotal + lay->BiasRowOffset[l] + (n << 1);
                grad[bi]     = 0f;
                grad[bi + 1] = 0f;
            }
        }
    }
}