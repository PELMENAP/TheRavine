using System.Text;
using UnityEngine;

[RequireComponent(typeof(EntityManager))]
public sealed class ColonyCanvasView : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool showCanvas;
    [SerializeField, Range(1, 4)] private int pixelScale = 2;
    [SerializeField, Min(1)] private int columns = 4;
    [SerializeField, Min(1f)] private float differenceGain = 2f;
    [SerializeField] private int windowId = 9998;

    [Header("Colours (UI_STYLE tokens)")]
    [SerializeField] private Color panel = new Color(0.082f, 0.090f, 0.078f, 0.88f);
    [SerializeField] private Color inkPrimary = new Color(0.918f, 0.902f, 0.855f, 1f);
    [SerializeField] private Color inkSecondary = new Color(0.639f, 0.627f, 0.580f, 1f);
    [SerializeField] private Color frame = new Color(0.839f, 0.824f, 0.769f, 0.38f);
    [SerializeField] private Color selection = new Color(0.949f, 0.929f, 0.878f, 1f);

    private const float Gap = 8f;
    private const float LabelHeight = 16f;
    private const float TitleHeight = 22f;
    private const int StripImages = 5;

    private static readonly string[] QuadrantNames = { "language", "habits", "rhythm", "territory" };

    private EntityManager _manager;
    private Rect _window = new Rect(10f, 10f, 900f, 600f);
    private Texture2D[] _current;
    private Texture2D[] _previous;
    private Texture2D _difference;
    private Color32[] _differencePixels;
    private int _seenVersion = -1;
    private int _selectedA = -1;
    private int _selectedB = -1;
    private string _summary = string.Empty;
    private int _referenceVersion = -1;
    private int _referenceA = -2;
    private int _referenceB = -2;
    private int _summaryVersion = -1;
    private int _summaryA = -2;
    private int _summaryB = -2;
    private GUIStyle _label;

    private void OnEnable() => _manager = GetComponent<EntityManager>();

    private void OnDisable()
    {
        var canvas = _manager != null ? _manager.Canvas : null;
        if (canvas != null) canvas.ViewOpen = false;
    }

    private void OnDestroy()
    {
        if (_current != null)
            for (int i = 0; i < _current.Length; i++)
            {
                if (_current[i] != null) Destroy(_current[i]);
                if (_previous[i] != null) Destroy(_previous[i]);
            }
        if (_difference != null) Destroy(_difference);
    }

    private void OnGUI()
    {
        var canvas = _manager != null ? _manager.Canvas : null;
        if (canvas != null) canvas.ViewOpen = showCanvas;
        if (!showCanvas) return;

        InitStyle();
        float cell = ColonyCanvas.Size * pixelScale;
        int live = 0;
        if (canvas != null)
            for (int i = 0; i < canvas.SlotCount; i++)
            {
                var slot = canvas.SlotAt(i);
                if (slot != null && slot.Live && slot.Rendered) live++;
            }

        int rows = Mathf.Max(1, (live + columns - 1) / columns);
        float width = Mathf.Max(columns, StripImages) * (cell + Gap) + Gap;
        float height = TitleHeight + rows * (cell + LabelHeight + Gap) + Gap + cell + LabelHeight + 6 * LabelHeight + 2 * Gap;
        _window.width = width;
        _window.height = height;
        _window = GUI.Window(windowId, _window, id => DrawWindow(canvas, cell), "");
    }

    private void InitStyle()
    {
        if (_label != null) return;
        _label = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = false, wordWrap = false };
        _label.normal.textColor = inkPrimary;
    }

    private void DrawWindow(ColonyCanvas canvas, float cell)
    {
        FillRect(new Rect(0f, 0f, _window.width, _window.height), panel);
        GUI.DragWindow(new Rect(0f, 0f, 10000f, TitleHeight));

        if (canvas == null)
        {
            GUI.Label(new Rect(Gap, 4f, _window.width - 2f * Gap, TitleHeight), "COLONY CANVAS: enable simulationLogEnabled", _label);
            return;
        }

        EnsureTextures(canvas);
        if (_seenVersion != canvas.Version)
        {
            _seenVersion = canvas.Version;
            for (int i = 0; i < canvas.SlotCount; i++)
            {
                var slot = canvas.SlotAt(i);
                if (slot != null && slot.Live && slot.Rendered) Upload(ref _current[i], slot.Pixels);
            }
        }

        GUI.Label(new Rect(Gap, 4f, _window.width - 2f * Gap, TitleHeight),
            $"COLONY CANVAS   sample {canvas.Version}   left click: colony A, right click: colony B   observer view, changes nothing", _label);

        float y = TitleHeight + Gap;
        int n = 0;
        for (int i = 0; i < canvas.SlotCount; i++)
        {
            var slot = canvas.SlotAt(i);
            if (slot == null || !slot.Live || !slot.Rendered) continue;

            float x = Gap + (n % columns) * (cell + Gap);
            float top = y + (n / columns) * (cell + LabelHeight + Gap);
            var rect = new Rect(x, top, cell, cell);
            GUI.DrawTexture(rect, _current[i], ScaleMode.StretchToFill, false);
            DrawFrame(rect, i == _selectedA ? selection : i == _selectedB ? inkSecondary : frame);
            GUI.Label(new Rect(x, top + cell, cell, LabelHeight), $"{slot.ColonyId} {ColonyArms.Name(slot.Arm)}", _label);
            HandleClick(rect, i);
            n++;
        }

        float stripTop = y + Mathf.Max(1, (n + columns - 1) / columns) * (cell + LabelHeight + Gap) + Gap;
        DrawReferences(canvas, stripTop, cell);
    }

    private void HandleClick(Rect rect, int index)
    {
        var e = Event.current;
        if (e.type != EventType.MouseDown || !rect.Contains(e.mousePosition)) return;
        if (e.button == 0) _selectedA = index;
        else if (e.button == 1) _selectedB = index;
        e.Use();
    }

    private void DrawReferences(ColonyCanvas canvas, float top, float cell)
    {
        var a = canvas.SlotAt(_selectedA);
        var b = canvas.SlotAt(_selectedB);
        var none = canvas.FirstNullPolicy();
        bool hasA = a != null && a.Live && a.Rendered;
        bool hasB = b != null && b.Live && b.Rendered;

        if (_referenceVersion != canvas.Version || _referenceA != _selectedA || _referenceB != _selectedB)
        {
            _referenceVersion = canvas.Version;
            _referenceA = _selectedA;
            _referenceB = _selectedB;
            if (hasA && a.HasPrevious) Upload(ref _previous[_selectedA], a.PreviousPixels);
            if (hasA && hasB) UpdateDifference(a, b);
        }

        DrawImage(0, top, cell, hasA ? _current[_selectedA] : null, hasA ? $"A now ({a.ColonyId})" : "A: pick a colony");
        DrawImage(1, top, cell, hasA && a.HasPrevious ? _previous[_selectedA] : null, "A one sample earlier");
        DrawImage(2, top, cell, none != null ? _current[IndexOf(canvas, none)] : null, none != null ? $"null_policy ({none.ColonyId})" : "no null_policy colony");
        DrawImage(3, top, cell, hasB ? _current[_selectedB] : null, hasB ? $"B now ({b.ColonyId})" : "B: pick a colony");
        DrawImage(4, top, cell, hasA && hasB ? _difference : null, "difference |A - B|");

        if (_summaryVersion != canvas.Version || _summaryA != _selectedA || _summaryB != _selectedB)
        {
            _summaryVersion = canvas.Version;
            _summaryA = _selectedA;
            _summaryB = _selectedB;
            _summary = BuildSummary(a, hasA, b, hasB, none);
        }
        GUI.Label(new Rect(Gap, top + cell + LabelHeight + Gap, _window.width - 2f * Gap, 6f * LabelHeight), _summary, _label);
    }

    private void DrawImage(int slot, float top, float cell, Texture2D texture, string caption)
    {
        float x = Gap + slot * (cell + Gap);
        var rect = new Rect(x, top, cell, cell);
        if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        DrawFrame(rect, frame);
        GUI.Label(new Rect(x, top + cell, cell, LabelHeight), caption, _label);
    }

    private string BuildSummary(ColonyCanvas.Slot a, bool hasA, ColonyCanvas.Slot b, bool hasB, ColonyCanvas.Slot none)
    {
        var sb = new StringBuilder(512);
        sb.Append("Jensen-Shannon divergence, bits (pair A-B | A against its previous sample | A against null_policy)\n");
        if (!hasA || !hasB)
        {
            sb.Append("select colony A (left click) and colony B (right click)");
            return sb.ToString();
        }

        var pair = ColonyCanvas.Between(a.Current, b.Current);
        var self = a.HasPrevious ? ColonyCanvas.Between(a.Current, a.Previous) : default;
        var toNull = none != null ? ColonyCanvas.Between(a.Current, none.Current) : default;
        bool hasSelf = a.HasPrevious;
        bool hasNull = none != null && !ReferenceEquals(none, a);

        Line(sb, QuadrantNames[0] + "  token|context", pair.Language, self.Language, hasSelf, toNull.Language, hasNull);
        Line(sb, QuadrantNames[1] + "  action mix", pair.ActionMix, self.ActionMix, hasSelf, toNull.ActionMix, hasNull);
        Line(sb, QuadrantNames[1] + "  P(next|previous)", pair.Bigram, self.Bigram, hasSelf, toNull.Bigram, hasNull);
        Line(sb, QuadrantNames[2] + "  goal mix", pair.GoalMix, self.GoalMix, hasSelf, toNull.GoalMix, hasNull);
        Line(sb, QuadrantNames[2] + "  goal x phase", pair.GoalPhase, self.GoalPhase, hasSelf, toNull.GoalPhase, hasNull);
        Line(sb, QuadrantNames[3] + "  channels", pair.Territory, self.Territory, hasSelf, toNull.Territory, hasNull);
        return sb.ToString();
    }

    private static void Line(StringBuilder sb, string name, float pair, float self, bool hasSelf, float toNull, bool hasNull)
    {
        sb.Append(name.PadRight(28)).Append(Format(pair)).Append("  |  ").Append(hasSelf ? Format(self) : "-")
          .Append("  |  ").Append(hasNull ? Format(toNull) : "-").Append('\n');
    }

    private static string Format(float value) => float.IsNaN(value) ? "-" : value.ToString("0.000");

    private static int IndexOf(ColonyCanvas canvas, ColonyCanvas.Slot slot)
    {
        for (int i = 0; i < canvas.SlotCount; i++)
            if (ReferenceEquals(canvas.SlotAt(i), slot)) return i;
        return 0;
    }

    private void UpdateDifference(ColonyCanvas.Slot a, ColonyCanvas.Slot b)
    {
        _differencePixels ??= new Color32[ColonyCanvas.Size * ColonyCanvas.Size];
        for (int i = 0; i < _differencePixels.Length; i++)
        {
            var p = a.Pixels[i];
            var q = b.Pixels[i];
            _differencePixels[i] = new Color32(Diff(p.r, q.r), Diff(p.g, q.g), Diff(p.b, q.b), 255);
        }
        Upload(ref _difference, _differencePixels);
    }

    private byte Diff(byte x, byte y) => (byte)Mathf.Min(255f, Mathf.Abs(x - y) * differenceGain);

    private void EnsureTextures(ColonyCanvas canvas)
    {
        if (_current != null) return;
        _current = new Texture2D[canvas.SlotCount];
        _previous = new Texture2D[canvas.SlotCount];
    }

    private static void Upload(ref Texture2D texture, Color32[] pixels)
    {
        if (texture == null)
            texture = new Texture2D(ColonyCanvas.Size, ColonyCanvas.Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        texture.SetPixelData(pixels, 0);
        texture.Apply(false);
    }

    private static void FillRect(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawFrame(Rect rect, Color color)
    {
        FillRect(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, 1f), color);
        FillRect(new Rect(rect.x - 1f, rect.yMax, rect.width + 2f, 1f), color);
        FillRect(new Rect(rect.x - 1f, rect.y, 1f, rect.height), color);
        FillRect(new Rect(rect.xMax, rect.y, 1f, rect.height), color);
    }
}
