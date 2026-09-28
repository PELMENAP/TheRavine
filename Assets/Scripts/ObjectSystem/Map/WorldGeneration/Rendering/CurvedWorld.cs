using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.Generator
{
    public static class CurvedWorld
    {
        private static readonly int CurveParamsId = Shader.PropertyToID("_CurveParams");
        private static readonly int ViewFacingId = Shader.PropertyToID("_ViewFacing");

        public static float K { get; private set; }
        public static float Flat { get; private set; }

        public static void Configure(float radius, float flat)
        {
            K = radius > 0f ? 0.5f / radius : 0f;
            Flat = math.max(flat, 0f);
            SetOrigin(Vector3.zero);
        }

        public static void SetFacing(int facing) =>
            Shader.SetGlobalFloat(ViewFacingId, facing < 0 ? -1f : 1f);

        public static void SetOrigin(Vector3 origin) =>
            Shader.SetGlobalVector(CurveParamsId, new Vector4(K, Flat, origin.z, origin.y));

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
