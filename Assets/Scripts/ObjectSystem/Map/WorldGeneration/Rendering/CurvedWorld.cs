using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.Generator
{
    public static class CurvedWorld
    {
        private static readonly int CurveParamsId = Shader.PropertyToID("_CurveParams");
        private static readonly int CurveFocusId = Shader.PropertyToID("_CurveFocus");

        public static float K { get; private set; }
        public static float Flat { get; private set; }
        public static float FocusDistance { get; private set; }

        public static void Configure(float radius, float flat, float focusDistance)
        {
            K = radius > 0f ? 0.5f / radius : 0f;
            Flat = math.max(flat, 0f);
            FocusDistance = math.max(focusDistance, 0f);
            PushFlat();
        }

        public static void Push(float2 focus, float referenceHeight)
        {
            Shader.SetGlobalVector(CurveParamsId, new Vector4(K, Flat, 0f, 0f));
            Shader.SetGlobalVector(CurveFocusId, new Vector4(focus.x, focus.y, referenceHeight, 0f));
        }

        public static void PushFlat()
        {
            Shader.SetGlobalVector(CurveParamsId, Vector4.zero);
            Shader.SetGlobalVector(CurveFocusId, Vector4.zero);
        }

        public static float MaxDrop(float distance)
        {
            float d = math.max(distance - Flat, 0f);
            return K * d * d;
        }

        public static float VisibleDistance(float cameraHeight, float cameraBack, float peakHeight)
        {
            if (K <= 0f) return float.PositiveInfinity;

            float c = cameraBack + Flat;
            float tangent = -c + math.sqrt(c * c + cameraHeight / K);
            return Flat + tangent + math.sqrt(math.max(peakHeight, 0f) / K);
        }
    }
}
