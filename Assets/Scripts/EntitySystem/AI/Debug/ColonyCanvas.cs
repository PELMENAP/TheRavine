using System;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

using TheRavine.Base;

public sealed class ColonyCanvas
{
    public const int Size     = 100;
    public const int Quadrant = Size / 2;
    public const int Contexts = ColonySpeechTables.Contexts;
    public const int Tokens   = ColonySpeechTables.Vocab;
    public const int Actions  = ActionCatalog.Count;
    public const int Goals    = SharedHierarchicalBrain.GoalCount;
    public const int PhaseBins = ColonyStats.PhaseBins;
    public const int Channels = 3;

    private const int TokenRows   = 3;
    private const int HabitCell   = 4;
    private const int HabitOffset = (Quadrant - Actions * HabitCell) / 2;
    private const int GoalRows    = 10;
    private const int BandTop     = Goals * GoalRows;

    private static readonly float3[] ViridisAnchors =
    {
        new float3(68f, 1f, 84f), new float3(71f, 44f, 122f), new float3(59f, 81f, 139f), new float3(44f, 113f, 142f),
        new float3(33f, 144f, 141f), new float3(39f, 173f, 129f), new float3(92f, 200f, 99f), new float3(170f, 220f, 50f),
        new float3(253f, 231f, 37f),
    };

    private static readonly float3[] GoalColors =
    {
        new float3(51f, 204f, 51f), new float3(230f, 51f, 51f), new float3(230f, 204f, 26f), new float3(51f, 128f, 230f),
    };

    private static readonly float3 SeasonCool = new float3(66f, 110f, 160f);
    private static readonly float3 SeasonWarm = new float3(200f, 140f, 60f);

    public sealed class Data
    {
        public readonly float[] Language  = new float[Contexts * Tokens];
        public readonly float[] ActionMix = new float[Actions];
        public readonly float[] Bigram    = new float[Actions * Actions];
        public readonly float[] GoalMix   = new float[Goals];
        public readonly float[] Rhythm    = new float[Goals * PhaseBins];
        public readonly float[] Territory = new float[Channels * Quadrant * Quadrant];

        public void CopyFrom(Data other)
        {
            Array.Copy(other.Language, Language, Language.Length);
            Array.Copy(other.ActionMix, ActionMix, ActionMix.Length);
            Array.Copy(other.Bigram, Bigram, Bigram.Length);
            Array.Copy(other.GoalMix, GoalMix, GoalMix.Length);
            Array.Copy(other.Rhythm, Rhythm, Rhythm.Length);
            Array.Copy(other.Territory, Territory, Territory.Length);
        }

        public void Decay(float keep)
        {
            Scale(Language, keep);
            Scale(ActionMix, keep);
            Scale(Bigram, keep);
            Scale(GoalMix, keep);
            Scale(Rhythm, keep);
        }

        private static void Scale(float[] values, float k)
        {
            for (int i = 0; i < values.Length; i++) values[i] *= k;
        }
    }

    public sealed class Slot
    {
        public int ColonyId;
        public ColonyArm Arm;
        public bool Live;
        public bool Rendered;
        public bool HasPrevious;
        public readonly Data Current  = new();
        public readonly Data Previous = new();
        public readonly Color32[] Pixels         = new Color32[Size * Size];
        public readonly Color32[] PreviousPixels = new Color32[Size * Size];

        internal bool Primed;
        internal readonly int[] LastActions = new int[Actions];
        internal readonly int[] LastGoals   = new int[Goals];
        internal readonly int[] LastBigram  = new int[Actions * Actions];
        internal readonly int[] LastPhase   = new int[Goals * PhaseBins];
    }

    public struct Divergence
    {
        public float Language, ActionMix, Bigram, GoalMix, GoalPhase, Territory;
    }

    private readonly Slot[] _slots = new Slot[ColonyRegistry.MaxColonies];
    private readonly float[] _territoryMax = new float[Channels];

    public bool ViewOpen { get; set; }
    public int Version { get; private set; }
    public float Season { get; private set; }
    public int SlotCount => _slots.Length;

    public bool IsActive => ViewOpen || SimulationRules.Active.CanvasExportEnabled;

    public Slot SlotAt(int colonyIndex) => (uint)colonyIndex < (uint)_slots.Length ? _slots[colonyIndex] : null;

    public Slot FirstNullPolicy()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot != null && slot.Live && slot.Rendered && slot.Arm == ColonyArm.NullPolicy) return slot;
        }
        return null;
    }

    public static Divergence Between(Data a, Data b) => new Divergence
    {
        Language  = JensenShannon.Conditional(a.Language, b.Language, Contexts, Tokens),
        ActionMix = JensenShannon.Divergence(a.ActionMix, b.ActionMix),
        Bigram    = JensenShannon.Conditional(a.Bigram, b.Bigram, Actions, Actions),
        GoalMix   = JensenShannon.Divergence(a.GoalMix, b.GoalMix),
        GoalPhase = JensenShannon.Divergence(a.Rhythm, b.Rhythm),
        Territory = JensenShannon.Divergence(a.Territory, b.Territory),
    };

    public void Update(float time, ColonyRegistry colonies, SpeechBus speech, string stamp)
    {
        var rules = SimulationRules.Active;
        float keep = rules.CanvasDecay;
        int n = Math.Min(colonies.Count, _slots.Length);
        float languageMax = 0f, habitMax = 0f;
        Array.Clear(_territoryMax, 0, Channels);
        Season = SeasonClock.Value(time);

        for (int c = 0; c < n; c++)
        {
            var colony = colonies[c];
            if (colony.IsWild || (uint)colony.Index >= (uint)_slots.Length) continue;

            var slot = _slots[colony.Index] ??= new Slot();
            slot.ColonyId = colony.ColonyId;
            slot.Arm = colony.Arm;
            slot.Live = colony.MemberCount > 0;
            if (!slot.Live) continue;

            slot.Previous.CopyFrom(slot.Current);
            slot.HasPrevious = slot.Rendered;
            Accumulate(slot, colony, speech, keep);
            SampleTerritory(slot, colony, rules);

            languageMax = Math.Max(languageMax, MaxColumnTotal(slot.Current.Language));
            habitMax = Math.Max(habitMax, MaxRowTotal(slot.Current.Bigram));
            var territory = slot.Current.Territory;
            for (int ch = 0; ch < Channels; ch++)
            {
                int start = ch * Quadrant * Quadrant;
                for (int i = 0; i < Quadrant * Quadrant; i++) _territoryMax[ch] = Math.Max(_territoryMax[ch], territory[start + i]);
            }
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || !slot.Live) continue;
            if (slot.Rendered) Array.Copy(slot.Pixels, slot.PreviousPixels, slot.Pixels.Length);
            Render(slot, languageMax, habitMax);
            slot.Rendered = true;
        }

        Version++;
        if (rules.CanvasExportEnabled) Export(stamp, time);
    }

    private static void Accumulate(Slot slot, ColonyState colony, SpeechBus speech, float keep)
    {
        var d = slot.Current;
        d.Decay(keep);
        speech?.TablesOf(colony.Index)?.AddEmissionsTo(d.Language);

        var stats = colony.Stats;
        bool add = slot.Primed;
        slot.Primed = true;

        for (int a = 0; a < Actions; a++)
        {
            int v = stats.ActionsStarted[a];
            if (add) d.ActionMix[a] += v - slot.LastActions[a];
            slot.LastActions[a] = v;
        }
        for (int g = 0; g < Goals; g++)
        {
            int v = stats.GoalsStarted[g];
            if (add) d.GoalMix[g] += v - slot.LastGoals[g];
            slot.LastGoals[g] = v;
        }
        for (int i = 0; i < slot.LastBigram.Length; i++)
        {
            int v = stats.ActionBigrams[i];
            if (add) d.Bigram[i] += v - slot.LastBigram[i];
            slot.LastBigram[i] = v;
        }
        for (int i = 0; i < slot.LastPhase.Length; i++)
        {
            int v = stats.GoalPhase[i];
            if (add) d.Rhythm[i] += v - slot.LastPhase[i];
            slot.LastPhase[i] = v;
        }
    }

    private static void SampleTerritory(Slot slot, ColonyState colony, SimulationRules rules)
    {
        var nest = colony.Nest;
        var view = nest.View;
        float span = rules.CanvasTerritorySpan > 0f ? rules.CanvasTerritorySpan : (view.InvCell > 0f ? view.Size / view.InvCell : 1f);
        float step = span / Quadrant;
        float2 origin = nest.Position - 0.5f * span;
        var territory = slot.Current.Territory;

        for (int y = 0; y < Quadrant; y++)
        {
            float z = origin.y + (Quadrant - 1 - y + 0.5f) * step;
            for (int x = 0; x < Quadrant; x++)
            {
                var p = new float2(origin.x + (x + 0.5f) * step, z);
                int i = y * Quadrant + x;
                territory[i] = nest.FieldAt(ColonyChannel.Food, p);
                territory[Quadrant * Quadrant + i] = nest.FieldAt(ColonyChannel.Trail, p);
                territory[2 * Quadrant * Quadrant + i] = nest.FieldAt(ColonyChannel.Territory, p);
            }
        }
    }

    private static float MaxColumnTotal(float[] language)
    {
        float max = 0f;
        for (int c = 0; c < Contexts; c++)
        {
            float total = 0f;
            for (int t = 0; t < Tokens; t++) total += language[c * Tokens + t];
            max = Math.Max(max, total);
        }
        return max;
    }

    private static float MaxRowTotal(float[] bigram)
    {
        float max = 0f;
        for (int i = 0; i < Actions; i++)
        {
            float total = 0f;
            for (int j = 0; j < Actions; j++) total += bigram[i * Actions + j];
            max = Math.Max(max, total);
        }
        return max;
    }

    private void Render(Slot slot, float languageMax, float habitMax)
    {
        var px = slot.Pixels;
        var black = new Color32(0, 0, 0, 255);
        for (int i = 0; i < px.Length; i++) px[i] = black;

        DrawLanguage(px, slot.Current.Language, languageMax);
        DrawHabits(px, slot.Current.Bigram, habitMax);
        DrawRhythm(px, slot.Current.Rhythm);
        DrawTerritory(px, slot.Current.Territory);
    }

    private static void DrawLanguage(Color32[] px, float[] language, float max)
    {
        Span<float> token = stackalloc float[Tokens];
        float norm = 1f / math.log(1f + math.max(max, 1e-6f));
        for (int x = 0; x < Quadrant; x++)
        {
            int c0 = x * Contexts / Quadrant;
            int c1 = Math.Max(c0 + 1, (x + 1) * Contexts / Quadrant);
            token.Clear();
            float total = 0f;
            for (int c = c0; c < c1; c++)
                for (int t = 0; t < Tokens; t++)
                {
                    float v = language[c * Tokens + t];
                    token[t] += v;
                    total += v;
                }
            if (total <= 0f) continue;

            float brightness = math.saturate(math.log(1f + total) * norm);
            for (int t = 0; t < Tokens; t++)
            {
                var color = Shade(Viridis(token[t] / total), brightness);
                for (int r = 0; r < TokenRows; r++) Put(px, x, t * TokenRows + r, color);
            }
        }
    }

    private static void DrawHabits(Color32[] px, float[] bigram, float max)
    {
        float norm = 1f / math.log(1f + math.max(max, 1e-6f));
        for (int i = 0; i < Actions; i++)
        {
            float total = 0f;
            for (int j = 0; j < Actions; j++) total += bigram[i * Actions + j];
            if (total <= 0f) continue;

            float brightness = math.saturate(math.log(1f + total) * norm);
            for (int j = 0; j < Actions; j++)
            {
                var color = Shade(Viridis(bigram[i * Actions + j] / total), brightness);
                int x0 = Quadrant + HabitOffset + j * HabitCell;
                int y0 = HabitOffset + i * HabitCell;
                for (int dy = 0; dy < HabitCell; dy++)
                    for (int dx = 0; dx < HabitCell; dx++) Put(px, x0 + dx, y0 + dy, color);
            }
        }
    }

    private void DrawRhythm(Color32[] px, float[] rhythm)
    {
        Span<float> bin = stackalloc float[PhaseBins];
        float maxBin = 0f;
        for (int b = 0; b < PhaseBins; b++)
        {
            float total = 0f;
            for (int g = 0; g < Goals; g++) total += rhythm[g * PhaseBins + b];
            bin[b] = total;
            maxBin = Math.Max(maxBin, total);
        }
        if (maxBin <= 0f) return;

        float3 season = math.lerp(SeasonCool, SeasonWarm, math.saturate(0.5f * (Season + 1f)));
        for (int b = 0; b < PhaseBins; b++)
        {
            if (bin[b] <= 0f) continue;
            for (int g = 0; g < Goals; g++)
            {
                var color = Shade(GoalColors[g], rhythm[g * PhaseBins + b] / bin[b]);
                for (int r = 0; r < GoalRows; r++) Put(px, b, Quadrant + g * GoalRows + r, color);
            }
            var band = Shade(season, bin[b] / maxBin);
            for (int r = BandTop; r < Quadrant; r++) Put(px, b, Quadrant + r, band);
        }
    }

    private void DrawTerritory(Color32[] px, float[] territory)
    {
        float3 inv = new float3(
            _territoryMax[0] > 0f ? 1f / _territoryMax[0] : 0f,
            _territoryMax[1] > 0f ? 1f / _territoryMax[1] : 0f,
            _territoryMax[2] > 0f ? 1f / _territoryMax[2] : 0f);
        int plane = Quadrant * Quadrant;
        for (int y = 0; y < Quadrant; y++)
            for (int x = 0; x < Quadrant; x++)
            {
                int i = y * Quadrant + x;
                float3 v = math.saturate(new float3(territory[i], territory[plane + i], territory[2 * plane + i]) * inv);
                Put(px, Quadrant + x, Quadrant + y, new Color32((byte)(v.x * 255f), (byte)(v.y * 255f), (byte)(v.z * 255f), 255));
            }
    }

    private static void Put(Color32[] px, int x, int y, Color32 color) => px[(Size - 1 - y) * Size + x] = color;

    private static Color32 Shade(float3 rgb, float brightness)
    {
        float3 c = rgb * math.saturate(brightness);
        return new Color32((byte)c.x, (byte)c.y, (byte)c.z, 255);
    }

    private static float3 Viridis(float t)
    {
        float f = math.saturate(t) * (ViridisAnchors.Length - 1);
        int i = Math.Min((int)f, ViridisAnchors.Length - 2);
        return math.lerp(ViridisAnchors[i], ViridisAnchors[i + 1], f - i);
    }

    private void Export(string stamp, float time)
    {
        try
        {
            string dir = Path.Combine(NeuralModelStorage.BaseDirectory, $"canvas_{stamp}");
            Directory.CreateDirectory(dir);
            string suffix = ((long)time).ToString("D8");
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || !slot.Live || !slot.Rendered) continue;
                byte[] png = ImageConversion.EncodeArrayToPNG(slot.Pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)Size, (uint)Size);
                AtomicFile.WriteAllBytes(Path.Combine(dir, $"colony_{slot.ColonyId}_{suffix}.png"), png);
            }
        }
        catch (Exception e)
        {
            RavineLog.Warning($"ColonyCanvas: экспорт остановлен: {e.Message}");
        }
    }
}
