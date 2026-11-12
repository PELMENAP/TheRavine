using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ViewControl;

public class NalRegionTests
{
    private static readonly long Center = Position2Int.Pack(10, 20);

    private static readonly int[] AxialSectors = { 0, 4, 8, 12 };

    private static long Chunk(int dx, int dz) => Position2Int.Pack(10 + dx, 20 + dz);

    private static int View(int dx, int dz, int sector = 0, int rings = 3) =>
        MapGenerator.ViewRingOf(Center, ViewSector.FromIndex(sector), rings, Chunk(dx, dz));

    private static int Sim(int dx, int dz, int rings = 3) =>
        MapGenerator.SimRingOf(Center, rings, Chunk(dx, dz));

    private static int2 Rotate90(int dx, int dz) => new(dz, -dx);

    private static int LegacyView(int lateral, int forward, int rings)
    {
        lateral = math.abs(lateral);
        if (lateral <= 1 && math.abs(forward) <= 1) return 0;

        if (forward <= 1)
        {
            bool sideBlock = math.abs(forward) <= 1 && lateral <= 1 + 3;
            return sideBlock && rings >= 1 ? 1 : -1;
        }

        int ring = (forward + 1) / 3;
        if (ring > rings || lateral > ring * 3 + 1) return -1;
        return ring;
    }

    private static int2 AxialToWorld(int sector, int lateral, int forward)
    {
        ViewSector s = ViewSector.FromIndex(sector);
        float2 r = s.Right;
        float2 f = s.Forward;
        return new int2(
            (int)math.round(r.x * lateral + f.x * forward),
            (int)math.round(r.y * lateral + f.y * forward));
    }

    [Test]
    public void WindowChunksAreRingZeroInEverySector()
    {
        for (int s = 0; s < ViewSector.Count; s++)
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Assert.AreEqual(0, View(dx, dz, s), $"sector {s}");
            Assert.AreEqual(0, Sim(dx, dz));
        }
    }

    [Test]
    public void ChunksAheadFormBlocksPerRing()
    {
        for (int dz = 2; dz <= 4; dz++) Assert.AreEqual(1, View(0, dz));
        for (int dz = 5; dz <= 7; dz++) Assert.AreEqual(2, View(0, dz));
        for (int dz = 8; dz <= 10; dz++) Assert.AreEqual(3, View(0, dz));
    }

    [Test]
    public void BeyondTheLastRingIsOutside()
    {
        Assert.AreEqual(-1, View(0, 11));
        Assert.AreEqual(-1, View(0, 5, rings: 1));
    }

    [Test]
    public void BehindTheWindowIsOutsideOfTheView()
    {
        Assert.AreEqual(-1, View(0, -2));
        Assert.AreEqual(-1, View(0, -5));
        Assert.AreEqual(-1, View(3, -5));
    }

    [Test]
    public void SideBlocksOfAnAxialSectorAreRingOne()
    {
        for (int dx = 2; dx <= 4; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Assert.AreEqual(1, View(dx, dz));
            Assert.AreEqual(1, View(-dx, dz));
        }

        Assert.AreEqual(-1, View(3, 0, rings: 0));
        Assert.AreEqual(1, View(3, 0, rings: 1));
    }

    [Test]
    public void AxialWedgeIsASupersetOfTheLegacyFir()
    {
        for (int rings = 0; rings <= 4; rings++)
        foreach (int sector in AxialSectors)
        for (int lateral = -16; lateral <= 16; lateral++)
        for (int forward = -16; forward <= 16; forward++)
        {
            int legacy = LegacyView(lateral, forward, rings);
            if (legacy < 0) continue;

            int2 d = AxialToWorld(sector, lateral, forward);
            Assert.AreEqual(legacy, View(d.x, d.y, sector, rings),
                $"sector {sector} lateral {lateral} forward {forward} rings {rings}");
        }
    }

    [Test]
    public void AxialSectorsAreRotationsOfPlusZ()
    {
        for (int rings = 0; rings <= 4; rings++)
        foreach (int sector in AxialSectors)
        for (int lateral = -16; lateral <= 16; lateral++)
        for (int forward = -16; forward <= 16; forward++)
        {
            int2 d = AxialToWorld(sector, lateral, forward);
            Assert.AreEqual(View(lateral, forward, 0, rings), View(d.x, d.y, sector, rings),
                $"sector {sector} lateral {lateral} forward {forward} rings {rings}");
        }
    }

    [Test]
    public void QuarterTurnOfTheSectorRotatesTheSet()
    {
        for (int rings = 0; rings <= 4; rings++)
        for (int sector = 0; sector < ViewSector.Count; sector++)
        for (int dx = -16; dx <= 16; dx++)
        for (int dz = -16; dz <= 16; dz++)
        {
            int2 r = Rotate90(dx, dz);
            Assert.AreEqual(View(dx, dz, sector, rings), View(r.x, r.y, (sector + 4) % ViewSector.Count, rings),
                $"sector {sector} chunk {dx},{dz} rings {rings}");
        }
    }

    [Test]
    public void ZeroRingCountKeepsOnlyTheWindow()
    {
        Assert.AreEqual(0, View(1, 1, rings: 0));
        Assert.AreEqual(-1, View(0, 3, rings: 0));
        Assert.AreEqual(0, Sim(1, 1, rings: 0));
        Assert.AreEqual(-1, Sim(0, 3, rings: 0));
    }

    [Test]
    public void SimRingsAreChebyshevShells()
    {
        for (int d = 2; d <= 4; d++) Assert.AreEqual(1, Sim(d, 0));
        for (int d = 5; d <= 7; d++) Assert.AreEqual(2, Sim(d, 0));
        for (int d = 8; d <= 10; d++) Assert.AreEqual(3, Sim(d, 0));
        Assert.AreEqual(2, Sim(5, 5));
        Assert.AreEqual(1, Sim(4, -4));
        Assert.AreEqual(-1, Sim(11, 0));
        Assert.AreEqual(-1, Sim(0, -11));
        Assert.AreEqual(-1, Sim(10, 11));
    }

    [Test]
    public void SimRingsDoNotDependOnDirection()
    {
        for (int dx = -12; dx <= 12; dx++)
        for (int dz = -12; dz <= 12; dz++)
        {
            int expected = Sim(dx, dz);
            Assert.AreEqual(expected, Sim(-dx, dz));
            Assert.AreEqual(expected, Sim(dx, -dz));
            Assert.AreEqual(expected, Sim(dz, dx));
        }
    }

    [Test]
    public void SimRingsDoNotDependOnTheSector()
    {
        for (int sector = 0; sector < ViewSector.Count; sector++)
        for (int dx = -14; dx <= 14; dx++)
        for (int dz = -14; dz <= 14; dz++)
        {
            int view = View(dx, dz, sector, 4);
            if (view >= 0) Assert.AreEqual(Sim(dx, dz, 4), view, $"sector {sector} chunk {dx},{dz}");
        }
    }

    [Test]
    public void SimSquareCoversEverythingTheViewDraws()
    {
        const int rings = 4;
        for (int sector = 0; sector < ViewSector.Count; sector++)
        for (int dx = -20; dx <= 20; dx++)
        for (int dz = -20; dz <= 20; dz++)
        {
            int view = View(dx, dz, sector, rings);
            if (view < 0) continue;
            Assert.AreEqual(view, Sim(dx, dz, rings), $"sector {sector} {dx},{dz}");
        }
    }

    [Test]
    public void SimSquareHoldsEveryChunkOfTheLastRing()
    {
        const int rings = 4;
        int count = 0;
        for (int dx = -20; dx <= 20; dx++)
        for (int dz = -20; dz <= 20; dz++)
            if (Sim(dx, dz, rings) >= 0) count++;

        int side = 2 * (rings * MapGenerator.WindowSide + MapGenerator.chunkScale) + 1;
        Assert.AreEqual(side * side, count);
    }

    [Test]
    public void BlockOfChunkDeltaGroupsChunksByWindowSide()
    {
        for (int d = -1; d <= 1; d++) Assert.AreEqual(0, MapGenerator.BlockOfChunkDelta(d));
        for (int d = 2; d <= 4; d++) Assert.AreEqual(1, MapGenerator.BlockOfChunkDelta(d));
        for (int d = -4; d <= -2; d++) Assert.AreEqual(-1, MapGenerator.BlockOfChunkDelta(d));
        for (int d = 5; d <= 7; d++) Assert.AreEqual(2, MapGenerator.BlockOfChunkDelta(d));
        for (int d = -7; d <= -5; d++) Assert.AreEqual(-2, MapGenerator.BlockOfChunkDelta(d));
    }

    [Test]
    public void CollectListsExactlyTheBlocksOfTheWedgeNearestFirst()
    {
        var buffer = new ViewBlock[ViewWedge.MaxBlocks(4)];
        for (int s = 0; s < ViewSector.Count; s++)
        for (int rings = 0; rings <= 4; rings++)
        {
            ViewSector sector = ViewSector.FromIndex(s);
            int n = ViewWedge.Collect(sector, rings, buffer);

            int expected = 0;
            for (int bx = -rings; bx <= rings; bx++)
            for (int bz = -rings; bz <= rings; bz++)
                if ((bx != 0 || bz != 0) && ViewWedge.Contains(sector, bx, bz)) expected++;
            Assert.AreEqual(expected, n, $"sector {s} rings {rings}");

            for (int i = 1; i < n; i++)
                Assert.LessOrEqual(buffer[i - 1].Ring, buffer[i].Ring, $"sector {s} rings {rings}");
            Assert.LessOrEqual(n, ViewWedge.MaxBlocks(rings));
        }
    }


    private const float VerticalFov = 50f;
    private const float DesignAspect = 2.4f;
    private const float PitchMin = 15f;
    private const float PitchMax = 65f;
    private const float DistanceMax = 46f;
    private const float DistanceMin = 14f;
    private const float PivotSlack = 0.25f * MapGenerator.chunkSize * MapGenerator.WindowSide;

    [Test]
    public void WedgeCoversTheCameraFrameInEverySector()
    {
        const int rings = ObjectInfoRings;
        float edge = ViewSector.HalfDegrees + ViewSector.HysteresisDegrees;
        float[] yawOffsets = { -edge, -edge * 0.5f, 0f, edge * 0.5f, edge };
        float[] pitches = { PitchMin, 30f, 45f, PitchMax };
        float[] distances = { DistanceMin, 30f, DistanceMax };
        float[] pivots = { -PivotSlack, 0f, PivotSlack };

        int checkedHits = 0;
        for (int s = 0; s < ViewSector.Count; s++)
        {
            ViewSector sector = ViewSector.FromIndex(s);
            foreach (float offset in yawOffsets)
            foreach (float pitch in pitches)
            foreach (float distance in distances)
            foreach (float px in pivots)
            foreach (float pz in pivots)
                checkedHits += CheckFrame(sector, sector.Yaw + offset, pitch, distance, px, pz, rings);
        }

        Assert.Greater(checkedHits, 0);
    }

    private const int ObjectInfoRings = 4;

    private static int CheckFrame(ViewSector sector, float yawDegrees, float pitchDegrees, float distance,
        float pivotX, float pivotZ, int rings)
    {
        float yaw = math.radians(yawDegrees);
        float pitch = math.radians(pitchDegrees);
        float3 forward = new(math.sin(yaw) * math.cos(pitch), -math.sin(pitch), math.cos(yaw) * math.cos(pitch));
        float3 right = new(math.cos(yaw), 0f, -math.sin(yaw));
        float3 up = new(math.sin(yaw) * math.sin(pitch), math.cos(pitch), math.cos(yaw) * math.sin(pitch));
        float3 camera = new float3(pivotX, 0f, pivotZ) - forward * distance;

        float tanV = math.tan(math.radians(VerticalFov * 0.5f));
        float tanH = tanV * DesignAspect;
        const int steps = 24;
        int hits = 0;

        for (int i = 0; i <= steps; i++)
        for (int j = 0; j <= steps; j++)
        {
            float sx = (i / (float)steps * 2f - 1f) * tanH;
            float sy = (j / (float)steps * 2f - 1f) * tanV;
            float3 dir = forward + right * sx + up * sy;
            if (dir.y >= -1e-6f) continue;

            float t = -camera.y / dir.y;
            float3 hit = camera + dir * t;

            int dx = (int)math.floor(hit.x / MapGenerator.chunkSize + 0.5f);
            int dz = (int)math.floor(hit.z / MapGenerator.chunkSize + 0.5f);
            if (Sim(dx, dz, rings) < 0) continue;

            hits++;
            if (View(dx, dz, sector.Index, rings) < 0)
                Assert.Fail($"sector {sector.Index} yaw {yawDegrees:0.#} pitch {pitchDegrees:0.#} distance {distance:0.#} pivot {pivotX:0},{pivotZ:0}: chunk {dx},{dz} is in the frame but outside the wedge");
        }

        return hits;
    }


    [Test]
    public void SectorsAreSixteenAndCyclic()
    {
        Assert.AreEqual(16, ViewSector.Count);
        Assert.AreEqual(0, ViewSector.FromYaw(0f).Index);
        Assert.AreEqual(4, ViewSector.FromYaw(90f).Index);
        Assert.AreEqual(8, ViewSector.FromYaw(180f).Index);
        Assert.AreEqual(12, ViewSector.FromYaw(270f).Index);
        Assert.AreEqual(0, ViewSector.FromYaw(359f).Index);
        Assert.AreEqual(0, ViewSector.FromYaw(-5f).Index);
        Assert.AreEqual(3, ViewSector.FromIndex(15).Rotated(4).Index);
    }

    [Test]
    public void AxialSectorsHaveExactAxes()
    {
        Assert.AreEqual(new float2(0f, 1f), ViewSector.FromIndex(0).Forward);
        Assert.AreEqual(new float2(1f, 0f), ViewSector.FromIndex(4).Forward);
        Assert.AreEqual(new float2(0f, -1f), ViewSector.FromIndex(8).Forward);
        Assert.AreEqual(new float2(-1f, 0f), ViewSector.FromIndex(12).Forward);
        Assert.AreEqual(new float2(1f, 0f), ViewSector.FromIndex(0).Right);
        Assert.AreEqual(new float2(0f, -1f), ViewSector.FromIndex(4).Right);
    }

    [Test]
    public void SectorHysteresisHoldsTheCurrentSectorNearTheBoundary()
    {
        ViewSector zero = ViewSector.FromIndex(0);
        ViewSector one = ViewSector.FromIndex(1);

        Assert.AreEqual(zero, ViewSector.FromYaw(11.25f + 2f, zero));
        Assert.AreEqual(one, ViewSector.FromYaw(11.25f + 4f, zero));
        Assert.AreEqual(one, ViewSector.FromYaw(11.25f - 2f, one));
        Assert.AreEqual(zero, ViewSector.FromYaw(11.25f - 4f, one));
        Assert.AreEqual(zero, ViewSector.FromYaw(-11.25f - 2f, zero));
    }

    [Test]
    public void ViewOrientationRaisesViewChangedEveryTimeAndSectorChangedOnlyOnASectorChange()
    {
        ViewOrientation.Set(0f, 40f);

        int viewChanged = 0;
        int sectorChanged = 0;
        void OnView() => viewChanged++;
        void OnSector(ViewSector s) => sectorChanged++;

        ViewOrientation.ViewChanged += OnView;
        ViewOrientation.SectorChanged += OnSector;
        try
        {
            ViewOrientation.Set(1f, 40f);
            ViewOrientation.Set(2f, 40f);
            ViewOrientation.Set(2f, 41f);
            ViewOrientation.Set(2f, 41f);
            Assert.AreEqual(3, viewChanged);
            Assert.AreEqual(0, sectorChanged);

            ViewOrientation.Set(20f, 41f);
            Assert.AreEqual(4, viewChanged);
            Assert.AreEqual(1, sectorChanged);
            Assert.AreEqual(1, ViewOrientation.Sector.Index);
        }
        finally
        {
            ViewOrientation.ViewChanged -= OnView;
            ViewOrientation.SectorChanged -= OnSector;
            ViewOrientation.Set(0f, 40f);
        }
    }

    [Test]
    public void ViewOrientationMapsScreenInputToTheWorld()
    {
        ViewOrientation.Set(0f, 40f);
        Assert.AreEqual(0f, math.distance(ViewOrientation.ToWorld(new float2(0f, 1f)), new float2(0f, 1f)), 1e-5f);

        ViewOrientation.Set(90f, 40f);
        Assert.AreEqual(0f, math.distance(ViewOrientation.ToWorld(new float2(0f, 1f)), new float2(1f, 0f)), 1e-5f);
        Assert.AreEqual(0f, math.distance(ViewOrientation.ToWorld(new float2(1f, 0f)), new float2(0f, -1f)), 1e-5f);

        ViewOrientation.Set(45f, 40f);
        Assert.AreEqual(1f, math.length(ViewOrientation.ToWorld(new float2(1f, 1f))) / math.sqrt(2f), 1e-5f);

        ViewOrientation.Set(0f, 40f);
    }

    [Test]
    public void BillboardColumnIsTheNearestBakedSide()
    {
        float step = 360f / ViewOrientation.BakedSides;
        for (int k = 0; k < ViewOrientation.BakedSides; k++)
        {
            Assert.AreEqual(k, ViewOrientation.ColumnOf(k * step));
            Assert.AreEqual(k, ViewOrientation.ColumnOf(k * step + step * 0.4f));
            Assert.AreEqual(k, ViewOrientation.ColumnOf(k * step - step * 0.4f));
        }
        Assert.AreEqual(0, ViewOrientation.ColumnOf(359f));
    }
}
