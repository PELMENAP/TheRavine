using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Generator;

public class CloudNoiseTests
{
    private const int Size = 256;

    [Test]
    public void ValuesStayInUnitRangeAndSpreadOverIt()
    {
        float min = 1f, max = 0f;
        for (int y = 0; y < Size; y += 3)
        for (int x = 0; x < Size; x += 3)
        {
            float v = CloudNoise.Value(x, y, Size);
            Assert.GreaterOrEqual(v, 0f);
            Assert.LessOrEqual(v, 1f);
            min = math.min(min, v);
            max = math.max(max, v);
        }
        Assert.Less(min, 0.2f);
        Assert.Greater(max, 0.8f);
    }

    [Test]
    public void TextureTilesWithoutASeam()
    {
        for (int i = 0; i < Size; i += 7)
        {
            Assert.AreEqual(CloudNoise.Value(0, i, Size), CloudNoise.Value(Size, i, Size), 1e-6f);
            Assert.AreEqual(CloudNoise.Value(i, 0, Size), CloudNoise.Value(i, Size, Size), 1e-6f);
            Assert.Less(math.abs(CloudNoise.Value(Size - 1, i, Size) - CloudNoise.Value(0, i, Size)), 0.12f);
            Assert.Less(math.abs(CloudNoise.Value(i, Size - 1, Size) - CloudNoise.Value(i, 0, Size)), 0.12f);
        }
    }

    [Test]
    public void NeighbouringTexelsDifferLittle()
    {
        for (int y = 0; y < Size; y += 5)
        for (int x = 0; x < Size - 1; x += 5)
            Assert.Less(math.abs(CloudNoise.Value(x + 1, y, Size) - CloudNoise.Value(x, y, Size)), 0.12f);
    }

    [Test]
    public void ShadowRectIsSnappedToTexelsAndCentredOnTheSunShiftedPoint()
    {
        float2 origin = CloudShadowMap.SnapOrigin(new float2(1234.5f, -987.25f), 2048f, 128);
        Assert.AreEqual(0f, origin.x % 16f, 1e-3f);
        Assert.AreEqual(0f, origin.y % 16f, 1e-3f);
        Assert.AreEqual(1234.5f - 1024f, origin.x, 16f);

        float2 near = CloudShadowMap.SnapOrigin(new float2(1235.5f, -987.25f), 2048f, 128);
        Assert.AreEqual(origin.x, near.x, 1e-3f);

        float2 eye = new float2(100f, 200f);
        Assert.AreEqual(eye, CloudShadowMap.ShadowCenter(eye, 50f, 1000f, new float3(0f, 1f, 0f), true));
        Assert.AreEqual(eye, CloudShadowMap.ShadowCenter(eye, 50f, 1000f, new float3(1f, 0.5f, 0f), false));
        float2 shifted = CloudShadowMap.ShadowCenter(eye, 50f, 1000f, math.normalize(new float3(1f, 1f, 0f)), true);
        Assert.Greater(shifted.x, eye.x + 900f);
        Assert.AreEqual(eye.y, shifted.y, 1e-3f);
    }
}
