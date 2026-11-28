using UnityEngine;

namespace TheRavine.Base
{
    public static class WorldDayPhase
    {
        public static float Value;
        public static Light Sun;

        public static void Clear()
        {
            Value = 0f;
            Sun = null;
        }
    }
}
