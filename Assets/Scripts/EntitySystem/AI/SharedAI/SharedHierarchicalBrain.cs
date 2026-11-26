using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public partial class SharedHierarchicalBrain : IDisposable
{
    public enum Goal { Survive = 0, Hunt = 1, Forage = 2, Social = 3 }
    public const int GoalCount = 4;

    public static readonly int[][] ActionSubsets = ActionCatalog.BuildSubsets(GoalCount);

    private const int CoordDelaySteps  = 10;
    private const int ExecDelaySteps   = 3;

    public const int PlanCount = PlanCatalog.Count;

    public const int CoordAux = 2;

    public static int[] BuildCoordSizes(int combined) => new[] { combined, 32, 16, 16, PlanCount + 1 + CoordAux };

    public const int HeadingOutputs   = 2;
    public const int CurvatureOutputs = 1;
    public const int SpeechOutputs    = SimulationRules.SpeechLen * SimulationRules.SpeechVocab;
    public const int DecodeOutputs    = SimulationRules.SpeechContextBits;
    public const int CurvatureAux     = HeadingOutputs;
    public const int SpeechAux        = HeadingOutputs + CurvatureOutputs;
    public const int DirectionOutputs = (int)DirectionKind.Count;
    public const int StyleOutputs     = (int)AttackStyle.Count;
    public const int StyleAux         = HeadingOutputs + CurvatureOutputs;
    public const int TempoAux         = StyleAux + StyleOutputs;

    public static int DirectionAux(int goal)
        => goal == (int)Goal.Social ? SpeechAux + SpeechOutputs
         : goal == (int)Goal.Hunt   ? TempoAux + 1
         : 0;

    public static int DirOffsetAux(int goal) => DirectionAux(goal) + DirectionOutputs;

    public static int ExecAux(int goal) => DecodeOutputs + DirOffsetAux(goal) + 1;

    public static int[] BuildExecSizes(int combined, int goal, bool wideSocial = false)
    {
        int output = ActionSubsets[goal].Length + 1 + ExecAux(goal);
        return wideSocial && goal == (int)Goal.Social
            ? new[] { combined, 96, 48, 48, output }
            : new[] { combined, 64, 32, 32, output };
    }


    private readonly LSTMMemory          reservoir;
    private readonly DelayedPerceptron   coordinator;
    private readonly DelayedPerceptron[] executors;

    internal LSTMMemory          Reservoir   => reservoir;
    internal DelayedPerceptron   Coordinator => coordinator;
    internal DelayedPerceptron[] Executors   => executors;

    private readonly ValueCritic   coordCritic;
    private readonly ValueCritic[] execCritics;

    public readonly int   InputSize;
    public readonly int   LstmHidden;
    public readonly bool  WideSocialExecutor;
    public readonly int[] CoordLayerSizes;
    public readonly int[][] ExecLayerSizes;

    private readonly RandomNetworkDistillation _rnd;

    public readonly BrainDiagnostics GlobalDiagnostics = new();
    public int GlobalTrainingSteps { get; private set; }
    private readonly int[] _rareAvailable = new int[PlanCount * ActionCatalog.Count];
    private readonly int[] _rareSelected = new int[PlanCount * ActionCatalog.Count];

    private RunningMeanStd _rewardNorm;

    private PerceptronLayout   _coordCtxLayout;
    private PerceptronLayout[] _execCtxLayouts;
    

    public SharedHierarchicalBrain(int inputSize, int lstmHidden = 32) : this(inputSize, lstmHidden, false) { }

    public SharedHierarchicalBrain(int inputSize, int lstmHidden, bool wideSocialExecutor)
    {
        WideSocialExecutor = wideSocialExecutor;
        InputSize  = inputSize;
        LstmHidden = lstmHidden;
        int combined = inputSize + lstmHidden;

        CoordLayerSizes = BuildCoordSizes(combined);
        ExecLayerSizes  = new int[GoalCount][];
        for (int i = 0; i < GoalCount; i++)
            ExecLayerSizes[i] = BuildExecSizes(combined, i, WideSocialExecutor);

        reservoir   = new LSTMMemory(inputSize, lstmHidden, SimulationRules.Active.ReservoirSpectralRadius);
        coordinator = new DelayedPerceptron(CoordLayerSizes);

        executors = new DelayedPerceptron[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            executors[i] = new DelayedPerceptron(ExecLayerSizes[i]);

        coordCritic = new ValueCritic(combined);
        execCritics = new ValueCritic[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            execCritics[i] = new ValueCritic(combined);

        _rnd = new RandomNetworkDistillation(inputSize);

        ConfigureOptimizer();
        BuildContextLayouts();
        ApplyPendingGradients();
    }

    private const int TruncWindow       = 8;
    private const int DecisionRingSlack = 2;
    private const int CoordRingCapacity = CoordDelaySteps + DecisionRingSlack;
    private const int ExecRingCapacity  = ExecDelaySteps  + DecisionRingSlack;

    public float CurrentLearningRate { get; private set; }

    public bool NullPolicy { get; private set; }

    private GradientConflict _conflict;
    private int  _conflictStep = -1;
    private bool _conflictCycle;

    public GradientConflict Conflict => _conflict;

    private NoveltyProgressSystem _progress;
    private bool _progressNovelty;

    public bool ProgressNovelty => _progressNovelty && _progress != null;

    private JobHandle _pending;
    private bool      _trainingScheduled;

    private float _plateauScale = 1f;
    private float _plateauThreshold = float.NaN;

    public float ExplorationScale => _plateauScale;

    public EntityBrainContext CreateContext(GeneticParameters? p = null)
    {
        CompletePending();
        if (_coordCtxLayout == null) BuildContextLayouts();
        return new EntityBrainContext(InputSize, LstmHidden, _coordCtxLayout, _execCtxLayouts,
                                      p ?? GeneticParameters.Default);
    }
    private void BuildContextLayouts()
    {
        _coordCtxLayout = coordinator.BuildContextLayout(TruncWindow, CoordRingCapacity, CoordAux);
        _execCtxLayouts = new PerceptronLayout[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            _execCtxLayouts[i] = executors[i].BuildContextLayout(TruncWindow, ExecRingCapacity, ExecAux(i), DecodeOutputs);
    }

    public SharedHierarchicalBrain(SharedHierarchicalBrain src) : this(src.InputSize, src.LstmHidden, src.WideSocialExecutor)
    {
        src.CompletePending();
        reservoir.Dispose();
        coordinator.Dispose();
        reservoir   = new LSTMMemory(src.reservoir);
        coordinator = new DelayedPerceptron(src.coordinator);
        for (int i = 0; i < GoalCount; i++)
        {
            executors[i].Dispose();
            executors[i] = new DelayedPerceptron(src.executors[i]);
        }

        ConfigureOptimizer();
        BuildContextLayouts();
        ApplyPendingGradients();
    }

    private readonly float[] _goalMinDuration = BuildGoalDurations(false);
    private readonly float[] _goalMaxDuration = BuildGoalDurations(true);
    private static readonly int     BiasStride      = BuildBiasStride();

    private static float[] BuildGoalDurations(bool max)
    {
        var r = new float[GoalCount];
        for (int g = 0; g < GoalCount; g++)
        {
            var subset = ActionSubsets[g];
            float v = max ? 0f : float.MaxValue;
            for (int i = 0; i < subset.Length; i++)
                v = max ? math.max(v, ActionDurationTable.Max(subset[i]))
                        : math.min(v, ActionDurationTable.Min(subset[i]));
            r[g] = v;
        }
        return r;
    }

    private static int BuildBiasStride()
    {
        int s = PlanCount;
        for (int g = 0; g < GoalCount; g++) s = math.max(s, ActionSubsets[g].Length);
        return s;
    }

    private PerceptronBatch _batch;
    private readonly GoalBuckets _buckets = new();
    private NativeArray<KernelLayout> _kernels;
    private NativeArray<NetWeights>   _nets;

    private EntityBrainContext[] _dCtx      = Array.Empty<EntityBrainContext>();
    private float[]              _dTime     = Array.Empty<float>();
    private float[]              _dDt       = Array.Empty<float>();
    private int[]                _dRow      = Array.Empty<int>();
    private BrainDecision[]      _dDecision = Array.Empty<BrainDecision>();
    private bool[]               _dMade     = Array.Empty<bool>();
    private int[]                _dRnd      = Array.Empty<int>();
    private EntityBrainContext[] _rndCtx    = Array.Empty<EntityBrainContext>();
    private float[]              _rndReward = Array.Empty<float>();
    private NativeList<int>      _rndRows;
    private int _dCount;
    private int _rCount;

    public void Dispose()
    {
        CompletePending();
        coordinator.Dispose();
        reservoir.Dispose();
        for (int g = 0; g < GoalCount; g++)
            executors[g].Dispose();
        _rnd.Dispose();
        if (_rndRows.IsCreated) _rndRows.Dispose();
        _batch?.Dispose();
        _batch = null;
        if (_kernels.IsCreated) _kernels.Dispose();
        if (_nets.IsCreated)    _nets.Dispose();
    }

    public float GetCoordinatorEntropy(EntityBrainContext ctx) => ctx.CoordMLP.AverageEntropy;
    public Goal  GetCurrentGoal(EntityBrainContext ctx)        => ctx.CurrentGoal;

    public float GetExecutorEntropy(Goal goal, EntityBrainContext ctx)
        => ctx.ExecMLPs[(int)goal].AverageEntropy;


    private SharedHierarchicalBrain(LSTMMemory reservoir, DelayedPerceptron coordinator, DelayedPerceptron[] executors)
    {
        InputSize  = reservoir.InputSize;
        LstmHidden = reservoir.HiddenSize;
        WideSocialExecutor = executors[(int)Goal.Social].LayerSizes[1] != 64;

        this.reservoir   = reservoir;
        this.coordinator = coordinator;
        this.executors   = executors;

        CoordLayerSizes = coordinator.LayerSizes;
        ExecLayerSizes  = new int[GoalCount][];
        for (int i = 0; i < GoalCount; i++)
            ExecLayerSizes[i] = executors[i].LayerSizes;

        int combined = InputSize + LstmHidden;
        coordCritic = new ValueCritic(combined);
        execCritics = new ValueCritic[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            execCritics[i] = new ValueCritic(combined);

        _rnd = new RandomNetworkDistillation(InputSize);

        ConfigureOptimizer();
        BuildContextLayouts();
        ApplyPendingGradients();
    }
}