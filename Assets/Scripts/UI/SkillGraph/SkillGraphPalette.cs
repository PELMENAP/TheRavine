using UnityEngine;

public static class SkillGraphPalette
{
    public const float NodeSize = 46f;
    public const float RingWidth = 4f;
    public const float EdgeWidth = 2f;
    public const float BridgeWidth = 4f;
    public const float PotentialWidth = 1.5f;
    public const float DashLength = 6f;
    public const float DashGap = 5f;
    public const float PickTolerance = 8f;
    public const float ClickSlop = 4f;
    public const float ContentMargin = 56f;
    public const float MinZoom = 0.5f;
    public const float MaxZoom = 3f;
    public const float ZoomSpeed = 0.1f;

    public static readonly Color EdgeActive = new(0.74f, 0.84f, 0.96f, 1f);
    public static readonly Color EdgeBridge = new(0.96f, 0.80f, 0.45f, 1f);
    public static readonly Color EdgePotential = new(0.55f, 0.60f, 0.70f, 0.7f);
    public static readonly Color RingTrack = new(1f, 1f, 1f, 0.15f);
    public static readonly Color TalentLow = new(0.55f, 0.55f, 0.65f, 1f);
    public static readonly Color TalentHigh = new(0.45f, 0.95f, 0.65f, 1f);

    public static Color TalentColor(float talent) => Color.Lerp(TalentLow, TalentHigh, Mathf.Clamp01(talent));

    public static Color EdgeColor(SkillEdgeKind kind)
        => kind == SkillEdgeKind.Active ? EdgeActive : kind == SkillEdgeKind.Bridge ? EdgeBridge : EdgePotential;

    public static float EdgeWidthOf(SkillEdgeKind kind)
        => kind == SkillEdgeKind.Active ? EdgeWidth : kind == SkillEdgeKind.Bridge ? BridgeWidth : PotentialWidth;
}
