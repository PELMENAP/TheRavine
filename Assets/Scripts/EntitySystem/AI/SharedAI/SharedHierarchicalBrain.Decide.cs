using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public partial class SharedHierarchicalBrain
{
    public void BeginDecisionBatch()
    {
        CompletePending();
        for (int i = 0; i < _dCount; i++) _dCtx[i] = null;
        if (_rndRows.IsCreated)
        {
            for (int i = 0; i < _rndRows.Length; i++) _rndCtx[i] = null;
            _rndRows.Clear();
        }
        _dCount = 0;
        _rCount = 0;
        _batch?.ClearReservoir();
    }

    public unsafe int EnqueueDecision(EntityBrainContext ctx, float[] input, float simTime, float dt, bool allowDecision = true)
    {
        _batch ??= new PerceptronBatch(InputSize, BiasStride);

        int row = _rCount++;
        _batch.SetInput(row, input);
        _batch.AddReservoir(BuildReservoirItem(ctx.Reservoir, row, simTime, dt));

        bool decider = allowDecision && !ctx.ExecWindow.IsRunning(simTime);
        if (!decider && ctx.CoordDecisionId == 0) return -1;

        if (!_rndRows.IsCreated) _rndRows = new NativeList<int>(64, Allocator.Persistent);
        int k = _rndRows.Length;
        EnsureRndCapacity(k + 1);
        _rndRows.Add(row);
        _rndCtx[k] = ctx;
        if (!NullPolicy && !ctx.TrainOff) _rnd.Collect(input);
        if (!decider) return -1;

        int d = _dCount;
        EnsureDeciderCapacity(d + 1);

        _dCtx[d]  = ctx;
        _dTime[d] = simTime;
        _dDt[d]   = dt;
        _dRow[d]  = row;
        _dMade[d] = false;
        _dRnd[d]  = k;

        _dCount = d + 1;
        return d;
    }

    private static unsafe ReservoirItem BuildReservoirItem(LSTMContext res, int row, float simTime, float dt)
    {
        var item = new ReservoirItem { State = res.Ptr, Ema = res.Ema, InputRow = row, Alpha = 1f, Step = 1 };

        float hz = SimulationRules.Frame.ReservoirHz;
        if (hz <= 0f)
        {
            res.EmaPrimed = false;
            return item;
        }

        item.UseEma = 1;
        item.Alpha  = res.EmaPrimed ? 1f - math.exp(-math.max(dt, 0f) * hz) : 1f;
        res.EmaPrimed = true;

        if (simTime < res.NextStepTime)
        {
            item.Step = 0;
            return item;
        }

        float next = res.NextStepTime + 1f / hz;
        res.NextStepTime = next > simTime ? next : simTime + 1f / hz;
        return item;
    }

    public bool TryGetDecision(int slot, out BrainDecision decision)
    {
        if ((uint)slot < (uint)_dCount && _dMade[slot])
        {
            decision = _dDecision[slot];
            return true;
        }
        decision = default;
        return false;
    }

    public unsafe void RunDecisions()
    {
        CompletePending();

        if (_rCount == 0)
        {
            ScheduleTraining();
            return;
        }

        EnsureNativeTables();
        var batch = _batch;
        int n = _dCount;

        var reservoirHandle = batch.ScheduleReservoir(reservoir.WeightsPtr, reservoir.BiasesPtr, LstmHidden);

        int rndCount = _rndRows.IsCreated ? _rndRows.Length : 0;
        var rndHandle = rndCount > 0 ? _rnd.ScheduleInfer(batch.Inputs, _rndRows.AsArray(), rndCount) : default;

        if (n == 0)
        {
            rndHandle.Complete();
            ReadIntrinsic(rndCount);
            AccumulateNovelty(rndCount);
            _pending = reservoirHandle;
            ScheduleTraining();
            return;
        }

        float decay = ExplorationDecay();
        ref readonly var frame = ref SimulationRules.Frame;
        float coordEps = frame.CoordExplorationEpsilon;
        float execEps  = frame.ExecExplorationEpsilon;
        float coordGamma = frame.CoordGammaPerSecond;
        float execGamma  = frame.ExecGammaPerSecond;

        float maskBias = frame.PlanMaskBias;

        batch.ClearItems();
        for (int d = 0; d < n; d++)
        {
            var ctx = _dCtx[d];
            ctx.SkipExec = false;
            if (_dTime[d] < ctx.GoalEndTime) continue;

            FlushGoalRewardToCoordinator(ctx);

            var row = batch.BiasRow(d);
            row.Clear();
            for (int p = 0; p < PlanCount; p++)
                row[p] = ctx.CoordBias[p]
                       + (PlanCatalog.IsFeasible((PlanKind)p, ctx.PlanHints, ctx.CasteMask) ? 0f : -maskBias);

            var mlp  = ctx.CoordMLP;
            int slot = coordinator.BeginForward(mlp, _dDt[d]);

            batch.Add(new ForwardItem
            {
                Ctx         = mlp.Ptr,
                Lstm        = ctx.Reservoir.Ptr,
                Net         = 0,
                Slot        = slot,
                InputRow    = _dRow[d],
                BiasRow     = d,
                Owner       = d,
                Dt          = mlp.DeltaTime,
                Temperature = mlp.EffectiveTemperature,
            });
        }

        int coordCount = batch.Count;
        var coordHandle = coordCount > 0
            ? batch.Schedule(_kernels, _nets, LstmHidden, reservoirHandle)
            : reservoirHandle;

        JobHandle.CombineDependencies(coordHandle, rndHandle).Complete();

        ReadIntrinsic(rndCount);
        for (int d = 0; d < n; d++) _dCtx[d].IntrinsicReward = _rndReward[_dRnd[d]];

        for (int k = 0; k < coordCount; k++)
        {
            var item = batch.ItemAt(k);
            int d    = item.Owner;
            var ctx  = _dCtx[d];
            float simTime = _dTime[d];

            ctx.CoordMLP.Activation(0).CopyTo(ctx.CoordCombined);

            var planTicket = coordinator.FinishDecide(ctx.CoordCombined, ctx.CoordMLP, item.Slot,
                batch.BiasRow(d).Slice(0, PlanCount), CoordDelaySteps, coordCritic, ctx.Gamma(coordGamma), simTime,
                frame.PlanMinSeconds, frame.PlanMaxSeconds, coordEps, decay, false);

            if (NullPolicy)
            {
                planTicket.Predicted = DrawUniformPlan(ctx, planTicket.Predicted);
                planTicket.Trained   = true;
            }
            else if (ctx.TrainOff) planTicket.Trained = true;

            var plan = (PlanKind)planTicket.Predicted;
            planTicket.Duration = DelayedPerceptron.DurationFromLogit(planTicket.DurationLogit + planTicket.DurationNoise,
                frame.PlanMinSecondsByPlan[(int)plan], frame.PlanMaxSecondsByPlan[(int)plan]);
            ctx.CoordAux = new float2(planTicket.AuxValue[0], planTicket.AuxValue[1]);
            for (int p = 0; p < PlanCount; p++) ctx.PlanProbs[p] = planTicket.Probs[p];
            ctx.CurrentPlan     = plan;
            ctx.CurrentGoal     = PlanCatalog.GoalOf(plan);
            ctx.CoordDecisionId = planTicket.DecisionId;
            ctx.GoalEndTime     = simTime + planTicket.Duration;
            ctx.BeginGoal(simTime);
        }

        AccumulateNovelty(rndCount);

        for (int d = 0; d < n; d++)
        {
            var ctx = _dCtx[d];
            ctx.ExecMask = PlanCatalog.EntryMask(ctx.CurrentPlan, ctx.PlanHints) & ctx.CasteMask;
            if (ctx.ExecMask != 0 && ctx.CurrentPlan < PlanKind.Count) continue;

            ctx.SkipExec    = true;
            ctx.GoalBonus  += frame.PlanInfeasibleReward;
            ctx.GoalEndTime = 0f;
        }

        batch.ClearItems();
        _buckets.Build(_dCtx, n);

        for (int k = 0; k < _buckets.Count; k++)
        {
            int d   = _buckets.At(k);
            var ctx = _dCtx[d];
            int g   = (int)ctx.CurrentGoal;

            var subset  = ActionSubsets[g];
            var row     = batch.BiasRow(d);
            int allowed = 0;
            row.Clear();
            for (int j = 0; j < subset.Length; j++)
            {
                if ((ctx.ExecMask & (1 << subset[j])) != 0) allowed++;
                else row[j] = -maskBias;
            }

            if (allowed == 0)
            {
                ctx.SkipExec    = true;
                ctx.GoalBonus  += frame.PlanInfeasibleReward;
                ctx.GoalEndTime = 0f;
                continue;
            }
            ctx.ExecForced = allowed == 1;

            var mlp  = ctx.ExecMLPs[g];
            int slot = executors[g].BeginForward(mlp, _dDt[d]);

            batch.Add(new ForwardItem
            {
                Ctx         = mlp.Ptr,
                Lstm        = ctx.Reservoir.Ptr,
                Net         = 1 + g,
                Slot        = slot,
                InputRow    = _dRow[d],
                BiasRow     = d,
                Owner       = d,
                Dt          = mlp.DeltaTime,
                Temperature = mlp.EffectiveTemperature,
            });
        }

        int execCount = batch.Count;
        if (execCount > 0) batch.Schedule(_kernels, _nets, LstmHidden).Complete();

        for (int k = 0; k < execCount; k++)
        {
            var item = batch.ItemAt(k);
            int d    = item.Owner;
            int g    = item.Net - 1;
            var ctx  = _dCtx[d];
            float simTime = _dTime[d];

            var combined = ctx.ExecCombined[g];
            ctx.ExecMLPs[g].Activation(0).CopyTo(combined);

            bool trackRarity = !NullPolicy && frame.RareActionChance > 0f;
            int rare = trackRarity ? LeastSelectedAction(ctx.CurrentPlan, ctx.ExecMask, ActionSubsets[g]) : -1;
            float rareChance = rare >= 0 ? frame.RareActionChance * EnergySurplus(ctx.EnergyNorm, frame.CuriosityEnergyFloor) : 0f;
            var ticket = executors[g].FinishDecide(combined, ctx.ExecMLPs[g], item.Slot,
                batch.BiasRow(d).Slice(0, ActionSubsets[g].Length), ExecDelaySteps, execCritics[g], ctx.Gamma(execGamma),
                simTime, _goalMinDuration[g], _goalMaxDuration[g], execEps, decay, ctx.ExecForced, rare, rareChance);

            if (NullPolicy)
            {
                ticket.Predicted = DrawUniformAction(ctx.ExecMask, ActionSubsets[g], ticket.Predicted);
                ticket.Trained   = true;
            }
            else if (ctx.TrainOff) ticket.Trained = true;

            int action    = ActionSubsets[g][ticket.Predicted];
            if (trackRarity) _rareSelected[(int)ctx.CurrentPlan * ActionCatalog.Count + action]++;
            float clamped = math.clamp(ticket.Duration,
                ActionDurationTable.Min(action), ActionDurationTable.Max(action));
            ticket.Duration = clamped;

            ctx.ExecWindow.Begin(ticket.DecisionId, simTime, math.max(clamped, ctx.GoalEndTime - simTime));

            var aux = ticket.AuxValue;
            float2 heading   = float2.zero;
            float  curvature = 0f;
            ushort tokens    = 0;
            float  offset    = 0f;
            byte   direction = (byte)DirectionKind.None;
            byte   speechLen = 0;
            var    style     = AttackStyle.Pursue;
            float  tempo     = 0f;

            if (ctx.DecodeTime > ctx.DecodeDoneTime && frame.SpeechDecodeLossWeight > 0f)
            {
                ctx.DecodeDoneTime  = ctx.DecodeTime;
                ticket.DecodeValid  = true;
                ticket.DecodeTarget = ctx.DecodeTarget;

                int correct = 0;
                for (int b = 0; b < DecodeOutputs; b++)
                    if ((ticket.DecodeOut[b] > 0f) == (((ctx.DecodeTarget >> b) & 1) != 0)) correct++;
                SpeechMetrics.RecordDecode(correct, ctx.DecodeTarget);
            }

            ushort planDirections = PlanCatalog.DirectionMask(ctx.CurrentPlan, ctx.PlanHints);
            ushort availableDirections = (ushort)(ctx.DirectionMask & planDirections);
            if (availableDirections == 0) availableDirections = ctx.DirectionMask;
            direction = PickMasked(ticket, DirectionAux(g), DirectionOutputs, availableDirections);
            offset    = aux[DirOffsetAux(g)] * frame.DirMaxOffset;

            if (g == (int)Goal.Hunt || g == (int)Goal.Social)
            {
                heading   = new float2(ticket.HeadingSin, ticket.HeadingCos);
                curvature = aux[CurvatureAux];
                if (g == (int)Goal.Social)
                {
                    for (int s = 0; s < SimulationRules.SpeechLen; s++)
                    {
                        byte token = PickMasked(ticket, SpeechAux + s * SimulationRules.SpeechVocab,
                            SimulationRules.SpeechVocab, (1 << SimulationRules.SpeechVocab) - 1);
                        tokens = Utterance.SetToken(tokens, s, token < SimulationRules.SpeechVocab ? token : 0);
                    }
                    speechLen = (byte)SimulationRules.SpeechLen;
                }
                else if (g == (int)Goal.Hunt)
                {
                    byte pick = PickMasked(ticket, StyleAux, StyleOutputs, 0xFF);
                    if (pick < StyleOutputs) style = (AttackStyle)pick;
                    tempo = aux[TempoAux];
                }
            }

            _dDecision[d] = new BrainDecision(action, ticket.DecisionId, ctx.CoordDecisionId,
                ctx.CurrentGoal, simTime, clamped, in heading,
                curvature, tokens, ctx.CurrentPlan, ctx.GoalEndTime, direction, offset, speechLen,
                style: style, tempo: tempo, rareAction: ticket.RareAction);
            _dMade[d] = true;
        }

        ScheduleTraining();
    }

    private int LeastSelectedAction(PlanKind plan, int mask, int[] subset)
    {
        int offset = (int)plan * ActionCatalog.Count;
        int rare = -1;
        float lowest = float.MaxValue;
        for (int i = 0; i < subset.Length; i++)
        {
            int action = subset[i];
            if ((mask & (1 << action)) == 0) continue;
            int slot = offset + action;
            float share = (_rareSelected[slot] + 1f) / (++_rareAvailable[slot] + 1f);
            if (share >= lowest) continue;
            lowest = share;
            rare = i;
        }
        return rare;
    }

    private static int DrawUniformPlan(EntityBrainContext ctx, int fallback)
    {
        int count = 0;
        for (int p = 0; p < PlanCount; p++)
            if (PlanCatalog.IsFeasible((PlanKind)p, ctx.PlanHints, ctx.CasteMask)) count++;
        if (count == 0) return fallback;

        int pick = RavineRandom.RangeInt(0, count);
        for (int p = 0; p < PlanCount; p++)
        {
            if (!PlanCatalog.IsFeasible((PlanKind)p, ctx.PlanHints, ctx.CasteMask)) continue;
            if (pick-- == 0) return p;
        }
        return fallback;
    }

    private static int DrawUniformAction(int mask, int[] subset, int fallback)
    {
        int count = 0;
        for (int j = 0; j < subset.Length; j++)
            if ((mask & (1 << subset[j])) != 0) count++;
        if (count == 0) return fallback;

        int pick = RavineRandom.RangeInt(0, count);
        for (int j = 0; j < subset.Length; j++)
        {
            if ((mask & (1 << subset[j])) == 0) continue;
            if (pick-- == 0) return j;
        }
        return fallback;
    }

    private static byte PickMasked(DelayedItem ticket, int start, int count, int mask)
    {
        var aux   = ticket.AuxValue;
        var noise = ticket.AuxNoise;
        int best  = -1;
        float top = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            if ((mask & (1 << i)) == 0)
            {
                noise[start + i] = 0f;
                continue;
            }
            float v = aux[start + i];
            if (v <= top) continue;
            top  = v;
            best = i;
        }
        return best < 0 ? byte.MaxValue : (byte)best;
    }

    private void ScheduleTraining()
    {
        float clip = SimulationRules.Frame.PpoClipEpsilon;

        bool groups = ConflictCycle();
        var handle = coordinator.ScheduleTraining(clip);
        for (int g = 0; g < GoalCount; g++)
            handle = JobHandle.CombineDependencies(handle, executors[g].ScheduleTraining(clip, groups));

        _pending = JobHandle.CombineDependencies(_pending, handle);
        _trainingScheduled = true;
        JobHandle.ScheduleBatchedJobs();
    }

    private unsafe void EnsureNativeTables()
    {
        coordinator.EnsureNative();
        reservoir.EnsureNative();
        for (int g = 0; g < GoalCount; g++)
            executors[g].EnsureNative();

        if (!_kernels.IsCreated)
        {
            _kernels = new NativeArray<KernelLayout>(GoalCount + 1, Allocator.Persistent);
            _nets    = new NativeArray<NetWeights>(GoalCount + 1, Allocator.Persistent);

            _kernels[0] = *coordinator.KernelPtr;
            for (int g = 0; g < GoalCount; g++)
                _kernels[1 + g] = *executors[g].KernelPtr;
        }

        _nets[0] = new NetWeights { W = coordinator.WeightsPtr, B = coordinator.BiasesPtr };
        for (int g = 0; g < GoalCount; g++)
            _nets[1 + g] = new NetWeights { W = executors[g].WeightsPtr, B = executors[g].BiasesPtr };
    }

    private void EnsureRndCapacity(int needed)
    {
        if (needed <= _rndCtx.Length) return;
        int cap = Math.Max(_rndCtx.Length << 1, Math.Max(needed, 64));
        Array.Resize(ref _rndCtx, cap);
        Array.Resize(ref _rndReward, cap);
    }

    private void EnsureDeciderCapacity(int needed)
    {
        if (needed <= _dCtx.Length) return;
        int cap = Math.Max(_dCtx.Length << 1, needed);
        Array.Resize(ref _dCtx, cap);
        Array.Resize(ref _dTime, cap);
        Array.Resize(ref _dDt, cap);
        Array.Resize(ref _dRow, cap);
        Array.Resize(ref _dDecision, cap);
        Array.Resize(ref _dMade, cap);
        Array.Resize(ref _dRnd, cap);
    }
}