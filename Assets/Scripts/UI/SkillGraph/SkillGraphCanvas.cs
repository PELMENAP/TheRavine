using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SkillGraphCanvas : VisualElement
{
    private readonly List<SkillNodeElement> _nodes = new();
    private readonly Func<int, Vector2> _toScreen;
    private SkillGraphSnapshot _snapshot;
    private Vector2 _pan;
    private float _zoom = 1f;
    private Vector2 _downPosition;
    private bool _dragging;
    private bool _moved;

    public event Action<int> NodeClicked;
    public event Action<int> NodeHovered;
    public event Action<int> NodeUnhovered;
    public event Action<int, int, SkillEdgeKind> EdgeClicked;

    public float Zoom => _zoom;
    public Vector2 Pan => _pan;
    public int NodeCount => _nodes.Count;

    public SkillGraphCanvas()
    {
        AddToClassList("skill-canvas");
        style.flexGrow = 1f;
        pickingMode = PickingMode.Position;
        _toScreen = SlotToScreen;

        generateVisualContent += OnGenerate;
        RegisterCallback<GeometryChangedEvent>(_ => LayoutNodes());
        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<WheelEvent>(OnWheel);
    }

    public void Bind(SkillGraphSnapshot snapshot)
    {
        _snapshot = snapshot;
        while (_nodes.Count < snapshot.VertexCount) _nodes.Add(CreateNode());
        for (int i = 0; i < _nodes.Count; i++)
        {
            bool used = i < snapshot.VertexCount;
            _nodes[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
            if (used) _nodes[i].Set(snapshot, i);
        }
        LayoutNodes();
    }

    public void ResetView()
    {
        _pan = Vector2.zero;
        _zoom = 1f;
        LayoutNodes();
    }

    public Vector2 UnitToLocal(Vector2 unit)
    {
        Rect rect = contentRect;
        if (!(rect.width > 0f && rect.height > 0f)) return unit;
        float side = Mathf.Max(Mathf.Min(rect.width, rect.height) - 2f * SkillGraphPalette.ContentMargin, 1f);
        return rect.center + _pan + (unit - new Vector2(0.5f, 0.5f)) * side * _zoom;
    }

    private SkillNodeElement CreateNode()
    {
        var node = new SkillNodeElement();
        node.Clicked += skill => NodeClicked?.Invoke(skill);
        node.Hovered += skill => NodeHovered?.Invoke(skill);
        node.Unhovered += skill => NodeUnhovered?.Invoke(skill);
        Add(node);
        return node;
    }

    private Vector2 SlotToScreen(int slot) => UnitToLocal(_snapshot.Position[slot]);

    private void LayoutNodes()
    {
        if (_snapshot != null)
            for (int i = 0; i < _snapshot.VertexCount && i < _nodes.Count; i++) _nodes[i].PlaceAt(SlotToScreen(i));
        MarkDirtyRepaint();
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || evt.target != this) return;
        _dragging = true;
        _moved = false;
        _downPosition = evt.localPosition;
        this.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_dragging || !this.HasPointerCapture(evt.pointerId)) return;
        if (((Vector2)evt.localPosition - _downPosition).magnitude > SkillGraphPalette.ClickSlop) _moved = true;
        if (!_moved) return;
        _pan += (Vector2)evt.deltaPosition;
        LayoutNodes();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_dragging) return;
        _dragging = false;
        if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
        if (!_moved) PickEdgeAt(evt.localPosition);
    }

    private void OnWheel(WheelEvent evt)
    {
        float next = Mathf.Clamp(_zoom * Mathf.Exp(-evt.delta.y * SkillGraphPalette.ZoomSpeed), SkillGraphPalette.MinZoom, SkillGraphPalette.MaxZoom);
        if (Mathf.Approximately(next, _zoom)) return;

        Vector2 anchor = (Vector2)evt.localMousePosition - contentRect.center;
        _pan = anchor - (anchor - _pan) * (next / _zoom);
        _zoom = next;
        LayoutNodes();
        evt.StopPropagation();
    }

    private void PickEdgeAt(Vector2 point)
    {
        if (_snapshot == null) return;
        int edge = SkillGraphGeometry.NearestEdge(_snapshot, _toScreen, point, SkillGraphPalette.PickTolerance);
        if (edge >= 0) EdgeClicked?.Invoke(_snapshot.EdgeFrom[edge], _snapshot.EdgeTo[edge], _snapshot.EdgeKind[edge]);
    }

    private void OnGenerate(MeshGenerationContext context)
    {
        if (_snapshot == null) return;
        var painter = context.painter2D;
        for (int e = 0; e < _snapshot.EdgeCount; e++)
        {
            int from = _snapshot.SlotOf(_snapshot.EdgeFrom[e]);
            int to = _snapshot.SlotOf(_snapshot.EdgeTo[e]);
            if (from < 0 || to < 0) continue;

            SkillEdgeKind kind = _snapshot.EdgeKind[e];
            painter.lineWidth = SkillGraphPalette.EdgeWidthOf(kind);
            painter.strokeColor = SkillGraphPalette.EdgeColor(kind);
            Vector2 a = SlotToScreen(from), b = SlotToScreen(to);
            if (kind == SkillEdgeKind.Potential) DrawDashed(painter, a, b);
            else DrawLine(painter, a, b);
        }
    }

    private static void DrawLine(Painter2D painter, Vector2 a, Vector2 b)
    {
        painter.BeginPath();
        painter.MoveTo(a);
        painter.LineTo(b);
        painter.Stroke();
    }

    private static void DrawDashed(Painter2D painter, Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length <= 1e-3f) return;

        Vector2 direction = delta / length;
        float step = SkillGraphPalette.DashLength + SkillGraphPalette.DashGap;
        for (float at = 0f; at < length; at += step)
            DrawLine(painter, a + direction * at, a + direction * Mathf.Min(at + SkillGraphPalette.DashLength, length));
    }
}
