using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(EntityManager))]
public class SimulationDebugDashboard : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool showDashboard = true;
    [SerializeField] private int     windowId   = 9999;

    [Header("Sampling")]
    [SerializeField] private float sampleInterval = 1f;

    [Header("Colony")]
    [SerializeField, Min(0)] private int colonyIndex;

    private EntityManager _manager;

    private readonly RingBuffer<float> _populationHistory = new(120);
    private readonly RingBuffer<float> _avgEntropyHistory = new(120);
    private readonly RingBuffer<float> _avgRewardHistory  = new(120);

    private readonly RingBuffer<float>[] _goalHistory = new RingBuffer<float>[SharedHierarchicalBrain.GoalCount];
    private readonly float[]             _goalCounts  = new float[SharedHierarchicalBrain.GoalCount];

    private readonly GenerationTracker _generationTracker = new();

    private bool _energyConsumerRegistered;
    private double _nextSampleTime = double.NegativeInfinity;
    private double _startTime = double.NaN;
    private int    _totalBorn;
    private int    _totalDied;

    private static readonly string[] GoalNames  = { "Survive", "Hunt", "Forage", "Social" };
    private static readonly Color[]  GoalColors =
    {
        new Color(0.2f, 0.8f, 0.2f),
        new Color(0.9f, 0.2f, 0.2f),
        new Color(0.9f, 0.8f, 0.1f),
        new Color(0.2f, 0.5f, 0.9f),
    };

    private GUIStyle _headerStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _valueStyle;
    private bool     _stylesInitialized;

    private Rect _windowRect = new Rect(10, 10, 420, 1165);

    private readonly StringBuilder _speechText = new();
    private string _speechEntropy = "";
    private string _speechInfo    = "";
    private string _speechDecode  = "";
    private string _speechFlow    = "";
    private string _speechMute    = "";

    private static readonly string[] PopulationLabels = { "Alive", "Born total", "Died total", "Uptime" };
    private static readonly string[] BrainLabels = { "LR", "LR min", "Stale coord", "Stale exec", "Dropped rewards" };
    private static readonly string[] GenerationLabels = { "Best entropy", "Worst entropy", "Best lr", "Best temp", "Avg train steps" };
    private static readonly string[] FitnessLabels = { "Fitness P10/P50/P90", "Fitness max", "Fitness outliers" };
    private static readonly string[] LedgerLabels =
    {
        "Stock", "Body/Nest", "Food/Corpses", "Residual (sum)", "Soil stock", "Soil residual (sum)",
        "Warmth stock", "Warmth residual (sum)", "In/Out", "Food spawn/mature", "Spawn/Child start",
        "Starve ret/Clamp", "Viral/Rescale", "Basal/Move", "Digest loss", "Speech/Threaten", "Dash/Strike",
        "Reproduce/Rest heal", "Spoil/Death loss", "Yield/Overflow loss", "Store loss/Wilt",
        "Soil return/release", "Warmth emit/draw/decay", "Warmth draw ticks", "Metab credit (viral)"
    };
    private const int WarmthTicksRow = 23;
    private const int TopShown = 5;

    private readonly string[] _populationText = new string[PopulationLabels.Length];
    private readonly string[] _brainText = new string[BrainLabels.Length];
    private readonly string[] _generationText = new string[GenerationLabels.Length];
    private readonly string[] _fitnessText = new string[FitnessLabels.Length];
    private readonly string[] _ledgerText = new string[LedgerLabels.Length];
    private readonly string[] _goalBarText = new string[SharedHierarchicalBrain.GoalCount];
    private readonly string[] _topLines = new string[TopShown];
    private readonly List<EntityModel> _sortBuf = new(64);
    private string _avgEntropyText = "";
    private string _avgRewardText = "";
    private string _entropyGraphText = "";
    private string _rewardGraphText = "";
    private string _colonyLabel = "none";
    private bool _hasBrain;
    private bool _hasFitness;
    private bool _hasLedger;
    private int _topCount;
    private int _goalTotal = 1;
    private GUIStyle _entropyGraphStyle;
    private GUIStyle _rewardGraphStyle;

    private int _coordStaleDrops;
    private int _execStaleDrops;
    private int _coordTrainSteps;
    private int _execTrainSteps;

    private void SampleMetrics()
    {
        var entities = _manager.Entities;
        int count    = entities.Count;

        _populationHistory.Push(count);

        int coordStale = 0, execStale = 0, coordSteps = 0, execSteps = 0;

        if (count == 0)
        {
            _avgEntropyHistory.Push(0f);
            _avgRewardHistory.Push(0f);
            _coordStaleDrops = _execStaleDrops = _coordTrainSteps = _execTrainSteps = 0;
            BuildTexts();
            return;
        }

        float sumEntropy = 0f, sumReward = 0f;
        Array.Clear(_goalCounts, 0, _goalCounts.Length);

        float openFit = 0f, mutedFit = 0f;
        int openCount = 0, mutedCount = 0;

        foreach (var e in entities)
        {
            if (!e.IsWild)
            {
                if (e.Speech.Muted) { mutedFit += e.GetFitness(); mutedCount++; }
                else { openFit += e.GetFitness(); openCount++; }
            }

            var ctx = e.Brain.Context;
            var coord = ctx.CoordMLP;
            sumEntropy += coord.AverageEntropy;
            sumReward  += ctx.GoalRewardCount > 0
                ? ctx.GoalTotalReward / ctx.GoalRewardCount
                : 0f;
            _goalCounts[(int)ctx.CurrentGoal]++;

            coordStale += coord.Diagnostics.StaleSlotDrops;
            coordSteps += coord.TrainingSteps;

            var execs = ctx.ExecMLPs;
            for (int g = 0; g < execs.Length; g++)
            {
                execStale += execs[g].Diagnostics.StaleSlotDrops;
                execSteps += execs[g].TrainingSteps;
            }
        }

        _coordStaleDrops = coordStale;
        _execStaleDrops  = execStale;
        _coordTrainSteps = coordSteps;
        _execTrainSteps  = execSteps;

        _avgEntropyHistory.Push(sumEntropy / count);
        _avgRewardHistory.Push(sumReward  / count);

        for (int i = 0; i < SharedHierarchicalBrain.GoalCount; i++)
            _goalHistory[i].Push(_goalCounts[i] / count);

        _generationTracker.Record(entities, SelectedColony()?.Brain);
        FormatSpeech(openFit, openCount, mutedFit, mutedCount);
        BuildTexts();
    }

    private void FormatSpeech(float openFit, int openCount, float mutedFit, int mutedCount)
    {
        var sb = _speechText;

        sb.Clear();
        sb.Append(SpeechMetrics.TokenEntropy().ToString("F2")).Append(" / ")
          .Append(Mathf.Log(SimulationRules.SpeechVocab, 2f).ToString("F2")).Append(" bit (n=").Append(SpeechMetrics.TokenTotal).Append(')');
        _speechEntropy = sb.ToString();

        sb.Clear();
        sb.Append(SpeechMetrics.MeanMutualInformation().ToString("F3")).Append(" bit |");
        for (int b = 0; b < SimulationRules.SpeechContextBits; b++)
            sb.Append(' ').Append(SpeechMetrics.ContextNames[b][0]).Append(':').Append(SpeechMetrics.MutualInformation(b).ToString("F2"));
        _speechInfo = sb.ToString();

        sb.Clear();
        sb.Append((SpeechMetrics.DecodeAccuracy() * 100f).ToString("F1")).Append("% vs ")
          .Append((SpeechMetrics.DecodeBaseline() * 100f).ToString("F1")).Append("% (n=").Append(SpeechMetrics.DecodeEvents).Append(')');
        _speechDecode = sb.ToString();

        var bus = _manager.Speech;
        sb.Clear();
        if (bus != null)
            sb.Append(bus.Emitted).Append(" / ").Append(bus.Delivered).Append(" / ").Append(bus.Credited).Append(" / ").Append(bus.Dropped);
        _speechFlow = sb.ToString();

        sb.Clear();
        if (SimulationRules.Active.SpeechMuteSampling)
            sb.Append((mutedCount > 0 ? mutedFit / mutedCount : 0f).ToString("F3")).Append(" (").Append(mutedCount).Append(") vs ")
              .Append((openCount > 0 ? openFit / openCount : 0f).ToString("F3")).Append(" (").Append(openCount).Append(')');
        else sb.Append("off");
        _speechMute = sb.ToString();
    }

    private void BuildTexts()
    {
        var entities = _manager.Entities;
        _goalTotal = Mathf.Max(1, entities.Count);

        _populationText[0] = entities.Count.ToString();
        _populationText[1] = _totalBorn.ToString();
        _populationText[2] = _totalDied.ToString();
        _populationText[3] = FormatTime(double.IsNaN(_startTime) ? 0f : (float)(SimulationClock.TimeD - _startTime));

        float lastEntropy = _avgEntropyHistory.LastOrDefault();
        float lastReward  = _avgRewardHistory.LastOrDefault();
        _avgEntropyText   = lastEntropy.ToString("F3");
        _avgRewardText    = lastReward.ToString("F3");
        _entropyGraphText = $"E:{lastEntropy:F2}";
        _rewardGraphText  = $"R:{lastReward:F2}";

        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            float frac = _goalCounts[g] / _goalTotal;
            _goalBarText[g] = $"{GoalNames[g]}: {_goalCounts[g]:0}/{_goalTotal} ({frac * 100:0}%)";
        }

        var stats = _generationTracker.Current;
        _generationText[0] = stats.BestEntropy.ToString("F3");
        _generationText[1] = stats.WorstEntropy.ToString("F3");
        _generationText[2] = stats.BestLr.ToString("F4");
        _generationText[3] = stats.BestTemp.ToString("F3");
        _generationText[4] = stats.AvgTrainSteps.ToString("F0");

        var evo = _manager.Evolution;
        _hasFitness = evo != null;
        if (_hasFitness)
        {
            _fitnessText[0] = $"{evo.FitnessP10:F2} / {evo.FitnessP50:F2} / {evo.FitnessP90:F2}";
            _fitnessText[1] = evo.FitnessMax.ToString("F2");
            _fitnessText[2] = evo.FitnessOutliers.ToString();
        }

        BuildLedgerText();
        BuildSelectionTexts();
        BuildTopEntities(entities);
    }

    private void BuildSelectionTexts()
    {
        var colony = SelectedColony();

        _colonyLabel = colony == null
            ? "none"
            : $"#{colonyIndex} C{colony.ColonyId}{(colony.IsWild ? " (wild)" : "")} [{colony.MemberCount}]";

        var brain = colony?.Brain;
        _hasBrain = brain != null;
        if (_hasBrain)
        {
            var gd = brain.GlobalDiagnostics;
            _brainText[0] = brain.CurrentLearningRate.ToString("0.000000");
            _brainText[1] = SimulationRules.Active.MinLearningRate.ToString("0.000000");
            _brainText[2] = FormatDropRatio(_coordStaleDrops, _coordTrainSteps);
            _brainText[3] = FormatDropRatio(_execStaleDrops, _execTrainSteps);
            _brainText[4] = FormatDropRatio(gd.DroppedRewards, gd.AppliedRewards);
        }

        if (_hasLedger)
            _ledgerText[WarmthTicksRow] = colony != null ? colony.Stats.WarmthDrawTicks.ToString() : null;
    }

    private void BuildLedgerText()
    {
        var ledger = _manager.Energy;
        _hasLedger = ledger != null;
        if (!_hasLedger) return;

        var f = ledger.Last;
        var t = _ledgerText;
        t[0]  = ledger.Stock.ToString("F1");
        t[1]  = $"{ledger.StockEntities:F1} / {ledger.StockNest:F1}";
        t[2]  = $"{ledger.StockFood:F1} / {ledger.StockCorpses:F1}";
        t[3]  = $"{ledger.Residual:F2} ({ledger.ResidualSum:F1})";
        t[4]  = ledger.StockSoil.ToString("F1");
        t[5]  = $"{ledger.SoilResidual:F2} ({ledger.SoilResidualSum:F1})";
        t[6]  = ledger.StockWarmth.ToString("F1");
        t[7]  = $"{ledger.WarmthResidual:F2} ({ledger.WarmthResidualSum:F1})";
        t[8]  = $"{f.Inflow:F2} / {f.Outflow:F2}";
        t[9]  = $"{f.FoodSpawn:F2} / {f.FoodMaturation:F2}";
        t[10] = $"{f.SpawnStart:F2} / {f.ChildStart:F2}";
        t[11] = $"{f.StarvationReturn:F2} / {f.ClampGain:F2}";
        t[12] = $"{f.ViralNet:F2} / {f.RescaleNet:F2}";
        t[13] = $"{f.Basal:F2} / {f.Movement:F2}";
        t[14] = f.DigestionLoss.ToString("F2");
        t[15] = $"{f.Speech:F2} / {f.Threaten:F2}";
        t[16] = $"{f.Dash:F2} / {f.Strike:F2}";
        t[17] = $"{f.Reproduce:F2} / {f.RestHeal:F2}";
        t[18] = $"{f.CorpseSpoil:F2} / {f.DeathLoss:F2}";
        t[19] = $"{f.YieldLoss:F2} / {f.OverflowLoss:F2}";
        t[20] = $"{f.StoreLoss:F2} / {f.Wilt:F2}";
        t[21] = $"{f.SoilReturn:F2} / {f.SoilRelease:F2}";
        t[22] = $"{f.WarmthEmit:F2} / {f.WarmthDraw:F2} / {f.WarmthDecay:F2}";
        t[24] = f.MetabolismCredit.ToString("F2");
    }

    private static int CompareEntropyDescending(EntityModel a, EntityModel b) =>
        b.Brain.Context.CoordMLP.AverageEntropy.CompareTo(a.Brain.Context.CoordMLP.AverageEntropy);

    private void BuildTopEntities(IReadOnlyList<EntityModel> entities)
    {
        _topCount = Mathf.Min(TopShown, entities.Count);
        if (_topCount == 0) return;

        _sortBuf.Clear();
        for (int i = 0; i < entities.Count; i++) _sortBuf.Add(entities[i]);
        _sortBuf.Sort(CompareEntropyDescending);

        for (int i = 0; i < _topCount; i++)
        {
            var ctx = _sortBuf[i].Brain.Context;
            _topLines[i] = $"#{i + 1} G:{ctx.CurrentGoal,-7} "
                         + $"E:{ctx.CoordMLP.AverageEntropy:F2} "
                         + $"T:{ctx.CoordMLP.TrainingSteps}";
        }
    }

    private ColonyState SelectedColony()
    {
        var colonies = _manager.Colonies;
        if (colonies == null || colonies.Count == 0) return null;
        if (colonyIndex >= colonies.Count) colonyIndex = colonies.Count - 1;
        return colonies[colonyIndex];
    }

    private void DrawColonySelector(ref float y)
    {
        var colonies = _manager.Colonies;
        int count    = colonies != null ? colonies.Count : 0;

        if (GUI.Button(new Rect(8, y, 24, 14), "<") && count > 0)
        {
            colonyIndex = (colonyIndex - 1 + count) % count;
            BuildSelectionTexts();
        }
        if (GUI.Button(new Rect(36, y, 24, 14), ">") && count > 0)
        {
            colonyIndex = (colonyIndex + 1) % count;
            BuildSelectionTexts();
        }

        GUI.Label(new Rect(66, y, _windowRect.width - 70, 14), _colonyLabel, _labelStyle);
        y += 16f;
    }

    private void DrawWindow(int id)
    {
        float y = 4f;

        DrawSection(ref y, "POPULATION");
        for (int i = 0; i < PopulationLabels.Length; i++)
            DrawKV(ref y, PopulationLabels[i], _populationText[i]);
        DrawMiniGraph(ref y, _populationHistory, Color.cyan, 0f, _manager.MaxPopulation);

        DrawSection(ref y, "LEARNING");
        DrawColonySelector(ref y);
        DrawKV(ref y, "Avg entropy",  _avgEntropyText);
        DrawKV(ref y, "Avg reward",   _avgRewardText);

        if (_hasBrain)
        {
            for (int i = 0; i < BrainLabels.Length; i++)
                DrawKV(ref y, BrainLabels[i], _brainText[i]);
        }

        DrawDualGraph(ref y, _avgEntropyHistory, _avgRewardHistory,
            new Color(0.9f, 0.5f, 0.1f), Color.green, 0f, 2f);

        DrawSection(ref y, "GOAL DISTRIBUTION");
        DrawGoalBars(ref y);
        DrawGoalStackedGraph(ref y);

        DrawSection(ref y, "GENERATION STATS");
        DrawGenerationInfo(ref y);
        DrawFitnessDistribution(ref y);

        DrawSection(ref y, "ENERGY LEDGER");
        DrawEnergyLedger(ref y);

        DrawSection(ref y, "SPEECH");
        DrawKV(ref y, "Token entropy",   _speechEntropy);
        DrawKV(ref y, "MI token-ctx",    _speechInfo);
        DrawKV(ref y, "Decode acc",      _speechDecode);
        DrawKV(ref y, "Emit/Dlv/Cred/Drop", _speechFlow);
        DrawKV(ref y, "Muted vs open fit", _speechMute);

        DrawSection(ref y, "TOP ENTITIES");
        DrawTopEntities(ref y);

        GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
    }

    private static string FormatDropRatio(int drops, int accepted)
    {
        int   total = drops + accepted;
        float pct   = total > 0 ? drops * 100f / total : 0f;
        return $"{drops} ({pct:0.00}%)";
    }

    private void OnEnable() => SyncEnergyConsumer();

    private void OnDisable() => ReleaseEnergyConsumer();

    private void OnDestroy() => ReleaseEnergyConsumer();

    private void SyncEnergyConsumer()
    {
        if (_manager == null || showDashboard == _energyConsumerRegistered) return;

        if (showDashboard) _manager.RegisterEnergyConsumer();
        else _manager.UnregisterEnergyConsumer();
        _energyConsumerRegistered = showDashboard;
    }

    private void ReleaseEnergyConsumer()
    {
        if (!_energyConsumerRegistered) return;

        _energyConsumerRegistered = false;
        if (_manager != null) _manager.UnregisterEnergyConsumer();
    }

    private void Awake()
    {
        _manager = GetComponent<EntityManager>();
        for (int i = 0; i < SharedHierarchicalBrain.GoalCount; i++)
            _goalHistory[i] = new RingBuffer<float>(120);

        _manager.OnEntitySpawned += _ => _totalBorn++;
        _manager.OnEntityDied    += _ => _totalDied++;
    }

    private void Update()
    {
        SyncEnergyConsumer();
        double now = SimulationClock.TimeD;
        if (double.IsNaN(_startTime)) _startTime = now;
        if (now < _nextSampleTime) return;

        _nextSampleTime = now + sampleInterval;
        SampleMetrics();
    }

    private void OnGUI()
    {
        if (!showDashboard) return;
        InitStylesIfNeeded();

        _windowRect = GUI.Window(windowId, _windowRect, DrawWindow, "");
    }

    private void DrawHeader(ref float y, string text)
    {
        GUI.Label(new Rect(4, y, _windowRect.width - 8, 18), text, _headerStyle);
        y += 20f;
        DrawHLine(ref y, new Color(0.4f, 0.8f, 1f, 0.6f));
    }

    private void DrawSection(ref float y, string title)
    {
        y += 4f;
        DrawHLine(ref y, new Color(1f, 1f, 1f, 0.15f));
        GUI.Label(new Rect(4, y, _windowRect.width - 8, 14), title, _labelStyle);
        y += 15f;
    }

    private void DrawKV(ref float y, string key, string value)
    {
        GUI.Label(new Rect(8, y, 160, 14), key,   _labelStyle);
        GUI.Label(new Rect(170, y, _windowRect.width - 178, 14), value, _valueStyle);
        y += 15f;
    }

    private void DrawHLine(ref float y, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(4, y, _windowRect.width - 8, 1), Texture2D.whiteTexture);
        GUI.color = prev;
        y += 2f;
    }

    private void DrawMiniGraph(ref float y, RingBuffer<float> data, Color lineColor,
        float minV, float maxV, float height = 40f)
    {
        Rect bg = new Rect(8, y, _windowRect.width - 16, height);
        DrawGraphBackground(bg);

        var prev = GUI.color;
        GUI.color = lineColor;

        float[] arr   = data.ToArray();
        float   range = Mathf.Max(1f, maxV - minV);
        float   step  = bg.width / Mathf.Max(1, arr.Length - 1);

        for (int i = 1; i < arr.Length; i++)
        {
            float x0 = bg.x + (i - 1) * step;
            float x1 = bg.x + i       * step;
            float y0 = bg.yMax - (arr[i - 1] - minV) / range * bg.height;
            float y1 = bg.yMax - (arr[i]     - minV) / range * bg.height;
            DrawLine(x0, y0, x1, y1, lineColor, 1.5f);
        }

        GUI.color = prev;
        y += height + 4f;
    }

    private void DrawDualGraph(ref float y, RingBuffer<float> a, RingBuffer<float> b,
        Color ca, Color cb, float minV, float maxV, float height = 40f)
    {
        Rect bg = new Rect(8, y, _windowRect.width - 16, height);
        DrawGraphBackground(bg);

        DrawGraphLine(bg, a.ToArray(), ca, minV, maxV);
        DrawGraphLine(bg, b.ToArray(), cb, minV, maxV);

        _entropyGraphStyle ??= new GUIStyle(_labelStyle) { normal = { textColor = ca } };
        _rewardGraphStyle ??= new GUIStyle(_labelStyle) { normal = { textColor = cb } };

        GUI.Label(new Rect(bg.x + 2, bg.y + 2, 80, 12), _entropyGraphText, _entropyGraphStyle);
        GUI.Label(new Rect(bg.x + 86, bg.y + 2, 80, 12), _rewardGraphText, _rewardGraphStyle);

        y += height + 4f;
    }

    private void DrawGraphLine(Rect bg, float[] arr, Color col, float minV, float maxV)
    {
        float range = Mathf.Max(0.001f, maxV - minV);
        float step  = bg.width / Mathf.Max(1, arr.Length - 1);
        for (int i = 1; i < arr.Length; i++)
        {
            float x0 = bg.x + (i - 1) * step;
            float x1 = bg.x + i       * step;
            float y0 = bg.yMax - (arr[i - 1] - minV) / range * bg.height;
            float y1 = bg.yMax - (arr[i]     - minV) / range * bg.height;
            DrawLine(x0, y0, x1, y1, col, 1.5f);
        }
    }

    private void DrawGoalBars(ref float y)
    {
        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            float frac = _goalCounts[g] / _goalTotal;
            Rect  bg   = new Rect(8,  y, _windowRect.width - 16, 10);
            Rect  fill = new Rect(8,  y, (_windowRect.width - 16) * frac, 10);

            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = GoalColors[g];
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(12, y - 1, 200, 12), _goalBarText[g], _labelStyle);
            GUI.color = prev;
            y += 12f;
        }
        y += 2f;
    }

    private void DrawGoalStackedGraph(ref float y)
    {
        float height = 35f;
        Rect  bg     = new Rect(8, y, _windowRect.width - 16, height);
        DrawGraphBackground(bg);

        int len = _goalHistory[0].Length;
        if (len < 2) { y += height + 4f; return; }

        float step = bg.width / Mathf.Max(1, len - 1);

        for (int i = 1; i < len; i++)
        {
            float x0 = bg.x + (i - 1) * step;
            float x1 = bg.x + i       * step;
            float base0 = 0f, base1 = 0f;

            for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
            {
                float[] arr = _goalHistory[g].ToArray();
                float v0 = arr[i - 1], v1 = arr[i];
                float top0 = bg.yMax - (base0 + v0) * bg.height;

                Color fill = GoalColors[g];
                fill.a = 0.5f;
                DrawLine(x0, bg.yMax - base0 * bg.height, x0, top0, fill, step);
                base0 += v0; base1 += v1;
            }
        }

        y += height + 4f;
    }

    private void DrawGenerationInfo(ref float y)
    {
        for (int i = 0; i < GenerationLabels.Length; i++)
            DrawKV(ref y, GenerationLabels[i], _generationText[i]);
    }

    private void DrawFitnessDistribution(ref float y)
    {
        if (!_hasFitness) return;
        for (int i = 0; i < FitnessLabels.Length; i++)
            DrawKV(ref y, FitnessLabels[i], _fitnessText[i]);
    }

    private void DrawEnergyLedger(ref float y)
    {
        if (!_hasLedger) return;
        for (int i = 0; i < LedgerLabels.Length; i++)
        {
            if (_ledgerText[i] == null) continue;
            DrawKV(ref y, LedgerLabels[i], _ledgerText[i]);
        }
    }

    private void DrawTopEntities(ref float y)
    {
        for (int i = 0; i < _topCount; i++)
        {
            GUI.Label(new Rect(8, y, _windowRect.width - 12, 13), _topLines[i], _labelStyle);
            y += 13f;
        }
    }

    private void DrawGraphBackground(Rect r)
    {
        var prev = GUI.color;
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.07f);
        for (int i = 1; i < 4; i++)
        {
            float gy = r.y + r.height * i / 4f;
            GUI.DrawTexture(new Rect(r.x, gy, r.width, 1), Texture2D.whiteTexture);
        }
        GUI.color = prev;
    }

    private static void DrawLine(float x0, float y0, float x1, float y1, Color col, float width = 1f)
    {
        var prev = GUI.color;
        GUI.color = col;
        float dx = x1 - x0, dy = y1 - y0;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return;
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        var matrix  = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, new Vector2(x0, y0));
        GUI.DrawTexture(new Rect(x0, y0 - width * 0.5f, len, width), Texture2D.whiteTexture);
        GUI.matrix = matrix;
        GUI.color  = prev;
    }

    private static string FormatTime(float s)
    {
        int m = (int)(s / 60);
        int sec = (int)(s % 60);
        return $"{m:00}:{sec:00}";
    }

    private void InitStylesIfNeeded()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = new Color(0.4f, 0.85f, 1f) }
        };
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal   = { textColor = new Color(0.75f, 0.75f, 0.75f) }
        };
        _valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize   = 10,
            fontStyle  = FontStyle.Bold,
            normal     = { textColor = Color.white },
            alignment  = TextAnchor.MiddleRight
        };

        GUI.skin.window.normal.background = MakeTex(2, 2, new Color(0.06f, 0.07f, 0.1f, 0.95f));
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}

public class RingBuffer<T>
{
    private readonly T[] _buf;
    private int _head;
    public int Length => _buf.Length;

    public RingBuffer(int capacity) { _buf = new T[capacity]; }

    public void Push(T value) { _buf[_head] = value; _head = (_head + 1) % _buf.Length; }

    public T[] ToArray()
    {
        var result = new T[_buf.Length];
        for (int i = 0; i < _buf.Length; i++)
            result[i] = _buf[(_head + i) % _buf.Length];
        return result;
    }

    public T LastOrDefault()
    {
        int idx = (_head - 1 + _buf.Length) % _buf.Length;
        return _buf[idx];
    }
}

public class GenerationTracker
{
    public struct Stats
    {
        public float BestEntropy, WorstEntropy;
        public float BestLr, BestTemp;
        public float AvgTrainSteps;
    }

    public Stats Current { get; private set; }

    public void Record(IReadOnlyList<EntityModel> entities, SharedHierarchicalBrain brain)
    {
        if (entities.Count == 0) return;

        float best = float.MinValue, worst = float.MaxValue;
        float bestLr = 0f, bestTemp = 0f;
        float sumSteps = 0f;

        foreach (var e in entities)
        {
            var ctx = e.Brain.Context;
            float entropy = ctx.CoordMLP.AverageEntropy;

            if (entropy > best)
            {
                best     = entropy;
                bestLr   = ctx.CoordMLP.Params.BaseLearningRate;
                bestTemp = ctx.CoordMLP.Params.SoftmaxTemperature;
            }
            if (entropy < worst) worst = entropy;
            sumSteps += ctx.CoordMLP.TrainingSteps;
        }

        Current = new Stats
        {
            BestEntropy   = best,
            WorstEntropy  = worst,
            BestLr        = bestLr,
            BestTemp      = bestTemp,
            AvgTrainSteps = sumSteps / entities.Count,
        };
    }
}