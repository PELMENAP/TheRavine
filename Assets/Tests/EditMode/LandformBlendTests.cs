using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;

using TheRavine.Generator;

public class LandformBlendTests
{
    private const int Size = MapGenerator.mapChunkSize;
    private const int Cells = Size * Size;
    private const int Seed = 12345;
    private const float BlendRadius = 0.3f;
    private const float BaseWeight = 0.05f;
    private const float Rcp2 = 1f / (BlendRadius * BlendRadius);

    private static NativeArray<LandformParams> Bake(LandformSettings[] source)
    {
        var array = new NativeArray<LandformParams>(source.Length, Allocator.TempJob);
        for (int i = 0; i < source.Length; i++)
            array[i] = LandformParams.From(source[i]);
        return array;
    }

    private static void AssertIdentity(in LandformParams p)
    {
        Assert.AreEqual(1f, p.heightScale, 1e-6f);
        Assert.AreEqual(0f, p.heightOffset, 1e-6f);
        Assert.AreEqual(0f, p.ridgeWeight, 1e-6f);
        Assert.AreEqual(0f, p.warpAmplitude, 1e-6f);
        Assert.AreEqual(0f, p.terraceWeight, 1e-6f);
        Assert.AreEqual(0f, p.terraceStep, 1e-6f);
        Assert.AreEqual(1f, p.riverWidth, 1e-6f);
        Assert.AreEqual(1f, p.riverCarve, 1e-6f);
        Assert.AreEqual(0f, p.riverWarp, 1e-6f);
        Assert.AreEqual(1f, p.erosion, 1e-6f);
    }

    [Test]
    public void EmptyAndHillsOnlyGiveIdentity()
    {
        var empty = new NativeArray<LandformParams>(0, Allocator.TempJob);
        var hills = Bake(new[] { LandformPresets.Hills });
        var rng = new Unity.Mathematics.Random(7);

        for (int i = 0; i < 256; i++)
        {
            float c = rng.NextFloat();
            float r = rng.NextFloat();

            LandformBlend.Evaluate(c, r, empty, Rcp2, BaseWeight, out LandformParams a);
            LandformBlend.Evaluate(c, r, hills, Rcp2, BaseWeight, out LandformParams b);
            AssertIdentity(a);
            AssertIdentity(b);
        }

        empty.Dispose();
        hills.Dispose();
    }

    [Test]
    public void BlendedParametersAreContinuous()
    {
        var presets = Bake(LandformPresets.All);
        const float step = 1e-4f;

        var points = new System.Collections.Generic.List<float2>();
        for (int i = 0; i <= 64; i++)
            for (int j = 0; j <= 64; j++)
                points.Add(new float2(i / 64f, j / 64f));

        foreach (var s in LandformPresets.All)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float edge = BlendRadius * sign;
                points.Add(new float2(s.continentalness + edge, s.ruggedness));
                points.Add(new float2(s.continentalness, s.ruggedness + edge));
                points.Add(new float2(s.continentalness + edge - step * sign, s.ruggedness));
                points.Add(new float2(s.continentalness, s.ruggedness + edge - step * sign));
            }
        }

        foreach (float2 pt in points)
        {
            LandformBlend.Evaluate(pt.x, pt.y, presets, Rcp2, BaseWeight, out LandformParams a);
            LandformBlend.Evaluate(pt.x + step, pt.y, presets, Rcp2, BaseWeight, out LandformParams b);
            LandformBlend.Evaluate(pt.x, pt.y + step, presets, Rcp2, BaseWeight, out LandformParams d);

            AssertBounded(a, b);
            AssertBounded(a, d);
        }

        presets.Dispose();
    }

    private static void AssertBounded(in LandformParams a, in LandformParams b)
    {
        AssertClose(a.heightScale, b.heightScale, 1.7f);
        AssertClose(a.heightOffset, b.heightOffset, 0.7f);
        AssertClose(a.ridgeWeight, b.ridgeWeight, 1f);
        AssertClose(a.warpAmplitude, b.warpAmplitude, 45f);
        AssertClose(a.terraceWeight, b.terraceWeight, 1f);
        AssertClose(a.terraceWeight * a.terraceStep, b.terraceWeight * b.terraceStep, 0.12f);
        AssertClose(a.riverWidth, b.riverWidth, 1.8f);
        AssertClose(a.riverCarve, b.riverCarve, 1f);
        AssertClose(a.riverWarp, b.riverWarp, 40f);
        AssertClose(a.erosion, b.erosion, 2f);
    }

    private static void AssertClose(float a, float b, float span)
    {
        Assert.LessOrEqual(math.abs(a - b), 0.1f * span);
    }

    private static FastNoiseLite Build(in NoiseLayerSettings s, int seed)
    {
        var n = new FastNoiseLite(seed);
        n.SetNoiseType(s.noiseType);
        n.SetFractalType(s.fractalType);
        n.SetFrequency(s.frequency);
        n.SetFractalOctaves(s.octaves);
        n.SetFractalLacunarity(s.lacunarity);
        n.SetFractalGain(s.gain);
        n.SetFractalWeightedStrength(s.weightedStrength);
        return n;
    }

    private static void AssertHeightAndRiverMatchOldFormula(LandformSettings[] landforms)
    {
        var settings = new ChunkGenerationSettings { landforms = landforms };
        var noise = new Noise(settings, Seed);
        var offset = new Vector2Int(3, -2);

        var baked = Bake(landforms);
        var height = new NativeArray<float>(Cells, Allocator.TempJob);
        var river = new NativeArray<float>(Cells, Allocator.TempJob);
        var shape = new NativeArray<float2>(Cells, Allocator.TempJob);
        var erosion = new NativeArray<float>(Cells, Allocator.TempJob);
        var temperature = new NativeArray<float>(Cells, Allocator.TempJob);
        var moisture = new NativeArray<float>(Cells, Allocator.TempJob);

        noise.GenerateAllMaps(height, river, shape, erosion, temperature, moisture, baked, offset).Complete();

        var h1 = Build(settings.heightNoiseSettings, Seed);
        var h2 = Build(settings.heightNoiseSettings, Seed * 2);
        var rv = Build(settings.riverNoiseSettings, Seed * 3);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int wx = offset.x * Size + x;
                int wy = offset.y * Size + y;
                int i = y * Size + x;

                float expectedHeight = h1.GetNoise(wx, wy) * 0.5f + 0.5f + h2.GetNoise(wx, wy);
                float expectedRiver = rv.GetNoise(wx, wy) * 0.5f + 0.5f;

                Assert.AreEqual(expectedHeight, height[i], 1e-4f);
                Assert.AreEqual(expectedRiver, river[i], 1e-4f);
                Assert.AreEqual(1f, shape[i].x, 1e-5f);
                Assert.AreEqual(1f, shape[i].y, 1e-5f);
                Assert.AreEqual(1f, erosion[i], 1e-5f);
            }
        }

        baked.Dispose();
        height.Dispose();
        river.Dispose();
        shape.Dispose();
        erosion.Dispose();
        temperature.Dispose();
        moisture.Dispose();
    }

    [Test]
    public void HillsOnlyHeightAndRiverEqualOldFormula()
    {
        AssertHeightAndRiverMatchOldFormula(new[] { LandformPresets.Hills });
    }

    [Test]
    public void EmptyLandformsHeightAndRiverEqualOldFormula()
    {
        AssertHeightAndRiverMatchOldFormula(new LandformSettings[0]);
    }

    [Test]
    public void ErosionScaleOfCatchUpMatchesGenerate()
    {
        var settings = new ChunkGenerationSettings { landformBlendRadius = 10f };
        var noise = new Noise(settings, Seed);
        var offset = new Vector2Int(-5, 9);
        var baked = Bake(settings.landforms);

        var height = new NativeArray<float>(Cells, Allocator.TempJob);
        var river = new NativeArray<float>(Cells, Allocator.TempJob);
        var shape = new NativeArray<float2>(Cells, Allocator.TempJob);
        var fromGenerate = new NativeArray<float>(Cells, Allocator.TempJob);
        var fromCatchUp = new NativeArray<float>(Cells, Allocator.TempJob);
        var temperature = new NativeArray<float>(Cells, Allocator.TempJob);
        var moisture = new NativeArray<float>(Cells, Allocator.TempJob);

        noise.GenerateAllMaps(height, river, shape, fromGenerate, temperature, moisture, baked, offset).Complete();
        noise.GenerateErosionScale(fromCatchUp, baked, offset).Complete();

        bool differs = false;
        for (int i = 0; i < Cells; i++)
        {
            Assert.AreEqual(fromGenerate[i], fromCatchUp[i], 1e-6f);
            differs |= math.abs(fromGenerate[i] - 1f) > 1e-3f;
        }
        Assert.IsTrue(differs);

        baked.Dispose();
        height.Dispose();
        river.Dispose();
        shape.Dispose();
        fromGenerate.Dispose();
        fromCatchUp.Dispose();
        temperature.Dispose();
        moisture.Dispose();
    }

    private static void RunBiomeModifier(float riverWidth, float riverCarve, out float[] heightOut, out float[] riverBlendOut)
    {
        const int n = 16;
        var one = new float[n];
        for (int i = 0; i < n; i++) one[i] = 0.5f;

        var heightIn = new NativeArray<float>(one, Allocator.TempJob);
        var riverMap = new NativeArray<float>(one, Allocator.TempJob);
        var temperature = new NativeArray<float>(one, Allocator.TempJob);
        var moisture = new NativeArray<float>(one, Allocator.TempJob);
        var shape = new NativeArray<float2>(n, Allocator.TempJob);
        for (int i = 0; i < n; i++) shape[i] = new float2(riverWidth, riverCarve);

        var centresT = new NativeArray<float>(new[] { 0.5f }, Allocator.TempJob);
        var centresM = new NativeArray<float>(new[] { 0.5f }, Allocator.TempJob);
        var scale = new NativeArray<float>(new[] { 1f }, Allocator.TempJob);
        var offset = new NativeArray<float>(new[] { 0f }, Allocator.TempJob);
        var hasRiver = new NativeArray<byte>(new byte[] { 1 }, Allocator.TempJob);
        var bed = new NativeArray<float>(new[] { 0.05f }, Allocator.TempJob);
        var centre = new NativeArray<float>(new[] { 0.5f }, Allocator.TempJob);
        var radiusRcp = new NativeArray<float>(new[] { 5f }, Allocator.TempJob);

        var height = new NativeArray<float>(n, Allocator.TempJob);
        var blend = new NativeArray<float>(n, Allocator.TempJob);
        var climate = new NativeArray<float2>(n, Allocator.TempJob);

        new BiomeModifierJob
        {
            heightIn = heightIn,
            riverMap = riverMap,
            riverShape = shape,
            temperatureMap = temperature,
            moistureMap = moisture,
            biomeCentersT = centresT,
            biomeCentersM = centresM,
            biomeHeightScale = scale,
            biomeHeightOffset = offset,
            biomeHasRiver = hasRiver,
            riverBedHeight = bed,
            riverCenter = centre,
            riverRadiusRcp = radiusRcp,
            heightOut = height,
            climateOut = climate,
            riverBlendOut = blend,
            blendRadiusRcp2 = 1f / (0.25f * 0.25f),
            altitudeCooling = 0f
        }.Run(n);

        heightOut = height.ToArray();
        riverBlendOut = blend.ToArray();

        heightIn.Dispose();
        riverMap.Dispose();
        temperature.Dispose();
        moisture.Dispose();
        shape.Dispose();
        centresT.Dispose();
        centresM.Dispose();
        scale.Dispose();
        offset.Dispose();
        hasRiver.Dispose();
        bed.Dispose();
        centre.Dispose();
        radiusRcp.Dispose();
        height.Dispose();
        blend.Dispose();
        climate.Dispose();
    }

    [Test]
    public void ZeroRiverWidthRemovesRiverAndKeepsHeightsFinite()
    {
        RunBiomeModifier(0f, 1f, out var height, out var blend);
        for (int i = 0; i < height.Length; i++)
        {
            Assert.AreEqual(0f, blend[i], 1e-6f);
            Assert.IsTrue(float.IsFinite(height[i]));
            Assert.AreEqual(0.5f, height[i], 1e-4f);
        }
    }

    [Test]
    public void UnitRiverShapeKeepsRiverAndCarveScalesIt()
    {
        RunBiomeModifier(1f, 1f, out _, out var full);
        RunBiomeModifier(1f, 0.5f, out _, out var half);
        for (int i = 0; i < full.Length; i++)
        {
            Assert.Greater(full[i], 0.9f);
            Assert.AreEqual(full[i] * 0.5f, half[i], 1e-4f);
        }
    }
}
