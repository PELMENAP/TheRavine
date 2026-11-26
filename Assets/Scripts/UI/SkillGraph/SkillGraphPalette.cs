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
    public const float MenuWidth = 300f;
    public const float MenuHeightGuess = 240f;
    public const float NameWidth = 120f;

    public static readonly Color EdgeActive = new(0.863f, 0.843f, 0.780f, 1f);
    public static readonly Color EdgeBridge = new(0.820f, 0.478f, 0.259f, 1f);
    public static readonly Color EdgePotential = new(0.478f, 0.471f, 0.431f, 0.7f);
    public static readonly Color RingTrack = new(0.839f, 0.824f, 0.769f, 0.16f);
    public static readonly Color TalentLow = new(0.478f, 0.471f, 0.431f, 1f);
    public static readonly Color TalentHigh = new(0.384f, 0.600f, 0.639f, 1f);

    public static Color TalentColor(float talent) => Color.Lerp(TalentLow, TalentHigh, Mathf.Clamp01(talent));

    public static Color EdgeColor(SkillEdgeKind kind)
        => kind == SkillEdgeKind.Active ? EdgeActive : kind == SkillEdgeKind.Bridge ? EdgeBridge : EdgePotential;

    public static float EdgeWidthOf(SkillEdgeKind kind)
        => kind == SkillEdgeKind.Active ? EdgeWidth : kind == SkillEdgeKind.Bridge ? BridgeWidth : PotentialWidth;
}
