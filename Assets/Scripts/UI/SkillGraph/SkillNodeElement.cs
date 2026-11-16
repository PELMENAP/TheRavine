using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SkillNodeElement : VisualElement
{
    public const string ClassBase = "skill-node";
    public const string ClassOpen = "skill-node--open";
    public const string ClassAvailable = "skill-node--available";
    public const string ClassSilhouette = "skill-node--silhouette";
    public const string ClassIsland = "skill-node--island";
    public const string ClassPruned = "skill-node--pruned";
    public const string ClassBadge = "skill-node__badge";

    private static readonly string[] StateClasses = { null, ClassOpen, ClassAvailable, ClassSilhouette, ClassIsland, ClassPruned };

    private readonly Label _level = new();
    private readonly Label _clique = new();
    private readonly Label _decay = new();
    private readonly Label _name = new();
    private float _fraction;
    private Color _ring = SkillGraphPalette.TalentLow;

    public int Skill { get; private set; } = -1;
    public SkillVertexState State { get; private set; }

    public event Action<int> Clicked;
    public event Action<int> Hovered;
    public event Action<int> Unhovered;

    public SkillNodeElement()
    {
        AddToClassList(ClassBase);
        style.position = Position.Absolute;
        style.width = SkillGraphPalette.NodeSize;
        style.height = SkillGraphPalette.NodeSize;
        pickingMode = PickingMode.Position;

        _level.AddToClassList("skill-node__level");
        _level.pickingMode = PickingMode.Ignore;
        _clique.AddToClassList(ClassBadge);
        _clique.AddToClassList("skill-node__badge--clique");
        _clique.pickingMode = PickingMode.Ignore;
        _decay.AddToClassList(ClassBadge);
        _decay.AddToClassList("skill-node__badge--decay");
        _decay.pickingMode = PickingMode.Ignore;
        Add(_level);
        Add(_clique);
        Add(_decay);
        _name.AddToClassList("skill-node__name");
        _name.pickingMode = PickingMode.Ignore;
        _name.style.width = SkillGraphPalette.NameWidth;
        _name.style.left = (SkillGraphPalette.NodeSize - SkillGraphPalette.NameWidth) * 0.5f;
        _name.style.top = SkillGraphPalette.NodeSize + 2f;
        Add(_name);

        generateVisualContent += OnGenerate;
        RegisterCallback<ClickEvent>(_ => Clicked?.Invoke(Skill));
        RegisterCallback<PointerEnterEvent>(_ => Hovered?.Invoke(Skill));
        RegisterCallback<PointerLeaveEvent>(_ => Unhovered?.Invoke(Skill));
    }

    public void Set(SkillGraphSnapshot snapshot, int slot, string skillName)
    {
        _name.text = skillName;
        Skill = snapshot.Skill[slot];
        State = snapshot.State[slot];
        for (int i = 1; i < StateClasses.Length; i++) EnableInClassList(StateClasses[i], i == (int)State);

        bool known = State == SkillVertexState.Open || State == SkillVertexState.Available || State == SkillVertexState.Pruned;
        _level.text = known ? snapshot.Level[slot].ToString() : string.Empty;
        _clique.text = snapshot.Clique[slot] >= 3 ? snapshot.Clique[slot].ToString() : string.Empty;
        _decay.text = snapshot.Decay[slot] < 0.995f ? snapshot.Decay[slot].ToString("0.00") : string.Empty;
        _fraction = known ? Mathf.Clamp01(snapshot.Fraction[slot]) : 0f;
        _ring = SkillGraphPalette.TalentColor(snapshot.Talent[slot]);
        MarkDirtyRepaint();
    }

    public void PlaceAt(Vector2 center)
    {
        style.left = center.x - SkillGraphPalette.NodeSize * 0.5f;
        style.top = center.y - SkillGraphPalette.NodeSize * 0.5f;
    }

    private void OnGenerate(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        Vector2 center = contentRect.center;
        float radius = SkillGraphPalette.NodeSize * 0.5f - SkillGraphPalette.RingWidth;
        painter.lineWidth = SkillGraphPalette.RingWidth;
        painter.strokeColor = SkillGraphPalette.RingTrack;
        painter.BeginPath();
        painter.Arc(center, radius, 0f, 360f);
        painter.Stroke();
        if (_fraction <= 0f) return;

        painter.strokeColor = _ring;
        painter.BeginPath();
        painter.Arc(center, radius, -90f, -90f + 360f * _fraction);
        painter.Stroke();
    }
}
