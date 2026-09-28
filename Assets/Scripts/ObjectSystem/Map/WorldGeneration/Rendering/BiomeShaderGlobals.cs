using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.Generator
{
    public static class BiomeShaderGlobals
    {
        public const int MaxBiomes = 16;

        private static readonly int CentersId = Shader.PropertyToID("_BiomeCenters");
        private static readonly int TintId = Shader.PropertyToID("_BiomeTint");
        private static readonly int GrassTintId = Shader.PropertyToID("_BiomeGrassTint");
        private static readonly int CountId = Shader.PropertyToID("_BiomeCount");
        private static readonly int BlendRcp2Id = Shader.PropertyToID("_BiomeBlendRcp2");
        private static readonly int AlbedoId = Shader.PropertyToID("_BiomeAlbedo");

        private static readonly Vector4[] centers = new Vector4[MaxBiomes];
        private static readonly Vector4[] tints = new Vector4[MaxBiomes];
        private static readonly Vector4[] grassTints = new Vector4[MaxBiomes];
        private static int count;
        private static float blendRcp2;

        public static void Apply(ChunkGenerationSettings settings, Texture2DArray albedo)
        {
            BiomeSettings[] biomes = settings.biomesSettings;
            count = math.min(biomes?.Length ?? 0, MaxBiomes);
            if ((biomes?.Length ?? 0) > MaxBiomes)
                Debug.LogWarning($"[BiomeShaderGlobals] {biomes.Length} biomes, only {MaxBiomes} are sent to shaders");

            int layers = albedo != null ? albedo.depth : 1;

            for (int i = 0; i < MaxBiomes; i++)
            {
                if (i >= count)
                {
                    centers[i] = new Vector4(-10f, -10f, 0f, 0f);
                    tints[i] = Vector4.one;
                    grassTints[i] = Vector4.one;
                    continue;
                }

                BiomeSettings b = biomes[i];
                Vector2 c = b.Center;
                centers[i] = new Vector4(c.x, c.y, math.clamp(b.albedoLayer, 0, layers - 1), 0f);
                tints[i] = ToLinear(b.tint);
                grassTints[i] = ToLinear(b.grassTint);
            }

            float radius = math.max(settings.biomeBlendRadius, 1e-4f);
            blendRcp2 = 1f / (radius * radius);

            Shader.SetGlobalVectorArray(CentersId, centers);
            Shader.SetGlobalVectorArray(TintId, tints);
            Shader.SetGlobalVectorArray(GrassTintId, grassTints);
            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalFloat(BlendRcp2Id, blendRcp2);
            if (albedo != null) Shader.SetGlobalTexture(AlbedoId, albedo);
        }

        public static void ApplyTo(ComputeShader shader)
        {
            shader.SetVectorArray(CentersId, centers);
            shader.SetVectorArray(TintId, tints);
            shader.SetVectorArray(GrassTintId, grassTints);
            shader.SetFloat(CountId, count);
            shader.SetFloat(BlendRcp2Id, blendRcp2);
        }

        private static Vector4 ToLinear(Color color)
        {
            if (color.a <= 0f) return Vector4.one;
            Color linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            return new Vector4(linear.r, linear.g, linear.b, 1f);
        }
    }
}
