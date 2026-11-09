using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static class LevelSpace
    {
        public static float SnapY(float groundTop, int baseLevel, float levelHeight)
        {
            if (levelHeight <= 0f) return groundTop;
            return (math.ceil(groundTop / levelHeight) + baseLevel) * levelHeight;
        }

        public static bool NeedsFill(float objectY, float ground, float levelHeight) =>
            objectY > ground && objectY - ground < levelHeight;
    }
}
