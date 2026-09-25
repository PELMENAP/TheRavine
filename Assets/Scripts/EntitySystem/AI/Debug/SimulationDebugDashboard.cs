using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EntityManager))]
public class SimulationDebugDashboard : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool showDashboard = true;
    [SerializeField] private int     windowId   = 9999;

    [Header("Sampling")]
    [SerializeField] private float sampleInterval = 1f;

    private EntityManager _manager;

    private readonly RingBuffer<float> _populationHistory = new(120);
    private readonly RingBuffer<float> _avgEntropyHistory = new(120);
    private readonly RingBuffer<float> _avgRewardHistory  = new(120);

    private readonly RingBuffer<float>[] _goalHistory = new RingBuffer<float>[SharedHierarchicalBrain.GoalCount];
    private readonly float[]             _goalCounts  = new float[SharedHierarchicalBrain.GoalCount];

    private readonly GenerationTracker _generationTracker = new();

    private float _sampleTimer;
    private int   _totalBorn;
    private int   _totalDied;
    private float _simTime;

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

    private Rect _windowRect = new Rect(10, 10, 420, 980);
    private GUIStyle _warnStyle;

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
            return;
        }

        float sumEntropy = 0f, sumReward = 0f;
        Array.Clear(_goalCounts, 0, _goalCounts.Length);

        foreach (var e in entities)
        {
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

        _generationTracker.Record(entities, _manager.SharedBrain);
    }

    private void DrawWindow(int id)
    {
        float y = 4f;

        DrawSection(ref y, "POPULATION");
        DrawKV(ref y, "Alive",        _manager.Entities.Count.ToString());
        DrawKV(ref y, "Born total",   _totalBorn.ToString());
        DrawKV(ref y, "Died total",   _totalDied.ToString());
        DrawKV(ref y, "Uptime",       FormatTime(_simTime));
        DrawMiniGraph(ref y, _populationHistory, Color.cyan, 0f, _manager.MaxPopulation);

        DrawSection(ref y, "LEARNING");
        float lastEntropy = _avgEntropyHistory.LastOrDefault();
        float lastReward  = _avgRewardHistory.LastOrDefault();
        DrawKV(ref y, "Avg entropy",  lastEntropy.ToString("F3"));
        DrawKV(ref y, "Avg reward",   lastReward.ToString("F3"));

        var brain = _manager.SharedBrain;
        if (brain != null)
        {
            var gd = brain.GlobalDiagnostics;
            DrawKV(ref y, "LR",              brain.CurrentLearningRate.ToString("0.000000"));
            DrawKV(ref y, "LR min",          SimulationRules.Active.MinLearningRate.ToString("0.000000"));
            DrawKV(ref y, "Stale coord",     FormatDropRatio(_coordStaleDrops, _coordTrainSteps));
            DrawKV(ref y, "Stale exec",      FormatDropRatio(_execStaleDrops, _execTrainSteps));
            DrawKV(ref y, "Dropped rewards", FormatDropRatio(gd.DroppedRewards, gd.AppliedRewards));
        }

        DrawDualGraph(ref y, _avgEntropyHistory, _avgRewardHistory,
            new Color(0.9f, 0.5f, 0.1f), Color.green, 0f, 2f);

        DrawSection(ref y, "COLONIES");
        DrawColonies(ref y);

        DrawSection(ref y, "GOAL DISTRIBUTION");
        DrawGoalBars(ref y);
        DrawGoalStackedGraph(ref y);

        DrawSection(ref y, "GENERATION STATS");
        DrawGenerationInfo(ref y);

        DrawSection(ref y, "TOP ENTITIES");
        DrawTopEntities(ref y);

        GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
    }

    private static readonly Color[] ColonyColors =
    {
        new(0.30f, 0.65f, 1f), new(1f, 0.42f, 0.42f), new(0.42f, 1f, 0.54f), new(1f, 0.82f, 0.30f),
        new(0.78f, 0.49f, 1f), new(0.30f, 1f, 0.94f), new(1f, 0.62f, 0.30f), new(1f, 0.42f, 0.84f),
    };

    private static readonly Color[] PlanColors =
    {
        new(0.56f, 0.89f, 0.42f), new(0.89f, 0.77f, 0.42f), new(0.42f, 0.71f, 0.89f), new(0.89f, 0.42f, 0.42f),
        new(0.60f, 0.60f, 0.89f), new(1f, 0.55f, 0.26f), new(0.89f, 0.42f, 0.78f), new(0.42f, 0.89f, 0.82f),
    };

    private readonly int[] _planCounts = new int[PlanCatalog.Count];
    private GUIStyle _colonyStyle;

    private void DrawPlanBar(ref float y, System.ReadOnlySpan<int> members)
    {
        System.Array.Clear(_planCounts, 0, _planCounts.Length);
        int total = 0;
        for (int i = 0; i < members.Length; i++)
        {
            var kind = _manager.Entities[members[i]].Plan.Kind;
            if (kind >= PlanKind.Count) continue;
            _planCounts[(int)kind]++;
            total++;
        }

        Rect bg = new Rect(8, y, _windowRect.width - 16, 8);
        DrawGraphBackground(bg);
        if (total > 0)
        {
            var prev = GUI.color;
            float x = bg.x;
            for (int p = 0; p < _planCounts.Length; p++)
            {
                if (_planCounts[p] == 0) continue;
                float w = bg.width * _planCounts[p] / total;
                GUI.color = PlanColors[p];
                GUI.DrawTexture(new Rect(x, bg.y, w, bg.height), Texture2D.whiteTexture);
                x += w;
            }
            GUI.color = prev;
        }
        y += 10f;

        float lx = 8f;
        for (int p = 0; p < _planCounts.Length; p++)
        {
            if (_planCounts[p] == 0) continue;
            _colonyStyle.normal.textColor = PlanColors[p];
            GUI.Label(new Rect(lx, y, 60, 12), $"{PlanCatalog.Names[p]} {_planCounts[p]}", _colonyStyle);
            lx += 58f;
            if (lx > _windowRect.width - 60f) { lx = 8f; y += 12f; }
        }
        y += 14f;
    }

    private void DrawColonies(ref float y)
    {
        var colonies = _manager.Colonies;
        if (colonies == null) return;

        float interval = SimulationRules.Active.GenerationInterval;
        for (int c = 0; c < colonies.Count; c++)
        {
            var colony = colonies[c];
            var st     = colony.Stats;

            _colonyStyle.normal.textColor = ColonyColors[(colony.ColonyId - 1 & 0x7FFFFFFF) % ColonyColors.Length];
            GUI.Label(new Rect(8, y, 160, 14), $"C{colony.ColonyId} alive/born/died", _colonyStyle);
            GUI.Label(new Rect(170, y, 120, 14), $"{colony.MemberCount} / {st.Born} / {st.Died}", _valueStyle);
            y += 15f;
            DrawPlanBar(ref y, colony.Members);
            DrawKV(ref y, "  death rate /min", st.DeathRatePerMinute.ToString("F2"));
            DrawKV(ref y, "  lifespan mean / ema", $"{st.MeanLifespan:F0}s / {st.LifespanEma:F0}s");
            DrawKV(ref y, "  src brain/inst/plan",
                $"{st.SourceFraction(CommandSource.Brain) * 100f:0}/{st.SourceFraction(CommandSource.Instinct) * 100f:0}/{st.SourceFraction(CommandSource.Plan) * 100f:0}%");
            DrawKV(ref y, "  hunger / alarm", $"{colony.Nest.Hunger:F2} / {colony.Nest.Alarm:F2}");

            int migrating = 0;
            var members = colony.Members;
            for (int i = 0; i < members.Length; i++)
                if (_manager.Entities[members[i]].Plan.Kind == PlanKind.Migrate) migrating++;
            DrawKV(ref y, "  migration p / share / moves",
                $"{colony.Nest.MigrationPressure:F2} / {(members.Length > 0 ? migrating * 100f / members.Length : 0f):0}% / {colony.Nest.Relocations}");

            var cc = colony.CasteCounts;
            DrawKV(ref y, "  W/So/Sc/N/juv",
                $"{cc[(int)Caste.Worker]}/{cc[(int)Caste.Soldier]}/{cc[(int)Caste.Scout]}/{cc[(int)Caste.Nurse]}/{colony.JuvenileCount}");
            DrawKV(ref y, "  leader / colony POI",
                $"{(colony.Leader != null ? colony.Leader.EntityId.ToString() : "-")} / {colony.Pois.Count}");

            int started = 0, completed = 0;
            for (int p = 0; p < st.PlansStarted.Length; p++)
            {
                started   += st.PlansStarted[p];
                completed += st.PlansCompleted[p];
            }
            DrawKV(ref y, "  plans done", started > 0 ? $"{completed * 100f / started:0}% of {started}" : "-");

            if (st.Died > 0 && interval <= st.MeanLifespan)
            {
                GUI.Label(new Rect(8, y, _windowRect.width - 16, 14),
                    $"  ! GenerationInterval {interval:F0}s <= lifespan {st.MeanLifespan:F0}s", _warnStyle);
                y += 15f;
            }
        }
    }

    private static string FormatDropRatio(int drops, int accepted)
    {
        int   total = drops + accepted;
        float pct   = total > 0 ? drops * 100f / total : 0f;
        return $"{drops} ({pct:0.00}%)";
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
        _simTime    += Time.deltaTime;
        _sampleTimer += Time.deltaTime;

        if (_sampleTimer >= sampleInterval)
        {
            _sampleTimer = 0f;
            SampleMetrics();
        }
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
        GUI.Label(new Rect(170, y, 120, 14), value, _valueStyle);
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

        GUI.Label(new Rect(bg.x + 2, bg.y + 2, 80, 12),
            $"E:{a.LastOrDefault():F2}", new GUIStyle(_labelStyle) { normal = { textColor = ca } });
        GUI.Label(new Rect(bg.x + 86, bg.y + 2, 80, 12),
            $"R:{b.LastOrDefault():F2}", new GUIStyle(_labelStyle) { normal = { textColor = cb } });

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
        var entities = _manager.Entities;
        int total    = Mathf.Max(1, entities.Count);

        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            float frac = _goalCounts[g] / total;
            Rect  bg   = new Rect(8,  y, _windowRect.width - 16, 10);
            Rect  fill = new Rect(8,  y, (_windowRect.width - 16) * frac, 10);

            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = GoalColors[g];
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(12, y - 1, 200, 12),
                $"{GoalNames[g]}: {_goalCounts[g]:0}/{total} ({frac * 100:0}%)",
                _labelStyle);
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
                float top1 = bg.yMax - (base1 + v1) * bg.height;

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
        var stats = _generationTracker.Current;
        DrawKV(ref y, "Best entropy",   stats.BestEntropy.ToString("F3"));
        DrawKV(ref y, "Worst entropy",  stats.WorstEntropy.ToString("F3"));
        DrawKV(ref y, "Best lr",        stats.BestLr.ToString("F4"));
        DrawKV(ref y, "Best temp",      stats.BestTemp.ToString("F3"));
        DrawKV(ref y, "Avg train steps", stats.AvgTrainSteps.ToString("F0"));
    }

    private void DrawTopEntities(ref float y)
    {
        var entities = _manager.Entities;
        if (entities.Count == 0) return;

        int shown = Mathf.Min(5, entities.Count);
        var sorted = new List<EntityModel>(entities);
        sorted.Sort((a, b) =>
            b.Brain.Context.CoordMLP.AverageEntropy
             .CompareTo(a.Brain.Context.CoordMLP.AverageEntropy));

        for (int i = 0; i < shown; i++)
        {
            var e   = sorted[i];
            var ctx = e.Brain.Context;
            string line = $"#{i + 1} G:{ctx.CurrentGoal,-7} "
                        + $"E:{ctx.CoordMLP.AverageEntropy:F2} "
                        + $"T:{ctx.CoordMLP.TrainingSteps}";
            GUI.Label(new Rect(8, y, _windowRect.width - 12, 13), line, _labelStyle);
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
        _colonyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 10,
            fontStyle = FontStyle.Bold,
        };
        _warnStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 10,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = new Color(1f, 0.35f, 0.25f) }
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