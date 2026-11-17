using UnityEngine;
using UnityEngine.Rendering;
using TheRavine.Generator;
using TheRavine.ViewControl;

public partial class GrassSystem
{
    private sealed class FarBlock
    {
        public int Ring;
        public int Step;
        public float WidthScale;
        public ComputeBuffer Buffer;
        public MaterialPropertyBlock Block;
        public int ArgsOffset;
        public Bounds Bounds;
        public int Bx;
        public int Bz;
        public bool Active;
        public bool HasContent;
        public long Stamp;
    }

    private FarBlock[] far;
    private ViewBlock[] wanted;
    private int[] order;
    private int orderCount;
    private int activeRings;
    private bool farPoolReady;
    private bool sectorDirty;
    private bool hasFarCenter;
    private long farCenter;
    private long stamp;
    private MapGenerator farMap;
    private ComputeBuffer farArgsBuffer;

    private void OnSectorChanged(ViewSector newSector) => sectorDirty = true;

    private void BuildFarPool(MapGenerator map)
    {
        farPoolReady = true;
        activeRings = Mathf.Min(GrassLodBands.FarRings, map.ViewRingCount);
        if (activeRings <= 0) return;

        int total = 0;
        for (int ring = 1; ring <= activeRings; ring++)
            total += ViewWedge.MaxBlocksInRing(ring);
        if (total <= 0) return;

        int perCell = bladesPerCell * densityFactor;
        uint[] farArgs = new uint[total * argsStride];
        far = new FarBlock[total];
        wanted = new ViewBlock[System.Math.Max(ViewWedge.MaxBlocks(activeRings), 1)];
        order = new int[total];

        int slot = 0;
        for (int ring = 1; ring <= activeRings; ring++)
        {
            int step = GrassLodBands.ValidStep(lodCellSteps[ring - 1], MapGenerator.generationSize);
            float width = GrassLodBands.ValidWidthScale(lodWidthScales[ring - 1]);
            int capacity = GrassLodBands.Capacity(MapGenerator.generationSize, step, perCell, placedCapacity);
            Mesh mesh = ringMeshes[ring];

            for (int n = ViewWedge.MaxBlocksInRing(ring); n > 0; n--, slot++)
            {
                var block = new MaterialPropertyBlock();
                var buffer = new ComputeBuffer(System.Math.Max(capacity, 1), instanceStride, ComputeBufferType.Append);
                block.SetBuffer(InstanceDataId, buffer);

                int offset = slot * argsStride;
                farArgs[offset] = mesh.GetIndexCount(0);
                farArgs[offset + 2] = mesh.GetIndexStart(0);
                farArgs[offset + 3] = mesh.GetBaseVertex(0);

                far[slot] = new FarBlock
                {
                    Ring = ring,
                    Step = step,
                    WidthScale = width,
                    Buffer = buffer,
                    Block = block,
                    ArgsOffset = offset * sizeof(uint)
                };
            }
        }

        farArgsBuffer = new ComputeBuffer(farArgs.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
        farArgsBuffer.SetData(farArgs);
    }

    private void ApplySector()
    {
        sectorDirty = false;
        int count = ViewWedge.Collect(ViewOrientation.Sector, activeRings, wanted);

        for (int i = 0; i < far.Length; i++)
        {
            FarBlock slot = far[i];
            if (!slot.Active || IndexOfWanted(slot.Bx, slot.Bz, count) >= 0) continue;

            slot.Active = false;
            slot.Stamp = ++stamp;
        }

        orderCount = 0;
        for (int w = 0; w < count; w++)
        {
            ViewBlock block = wanted[w];
            int index = FindActive(block.X, block.Z);

            if (index < 0)
            {
                index = TakeSlot(block.X, block.Z, block.Ring);
                if (index < 0) continue;

                FarBlock slot = far[index];
                slot.HasContent = slot.HasContent && slot.Bx == block.X && slot.Bz == block.Z;
                slot.Bx = block.X;
                slot.Bz = block.Z;
                slot.Active = true;
            }

            order[orderCount++] = index;
        }
    }

    private int IndexOfWanted(int bx, int bz, int count)
    {
        for (int w = 0; w < count; w++)
            if (wanted[w].X == bx && wanted[w].Z == bz) return w;
        return -1;
    }

    private int FindActive(int bx, int bz)
    {
        for (int i = 0; i < far.Length; i++)
            if (far[i].Active && far[i].Bx == bx && far[i].Bz == bz) return i;
        return -1;
    }

    private int TakeSlot(int bx, int bz, int ring)
    {
        int best = -1;
        for (int i = 0; i < far.Length; i++)
        {
            FarBlock slot = far[i];
            if (slot.Ring != ring || slot.Active) continue;

            if (slot.HasContent && slot.Bx == bx && slot.Bz == bz) return i;
            if (best < 0 || slot.Stamp < far[best].Stamp) best = i;
        }
        return best;
    }

    private void BuildFar()
    {
        int limit = Mathf.Max(1, lodBlocksPerFrame);
        int built = 0;

        for (int k = 0; k < orderCount && built < limit; k++)
        {
            FarBlock slot = far[order[k]];
            if (!slot.Active || slot.HasContent) continue;

            long blockCenter = MapGenerator.BlockCenterChunk(farCenter, slot.Bx, slot.Bz);
            if (!farMap.TryBuildHeightWindow(blockCenter, out HeightWindow window)) continue;

            PlaceBlock(window, blockCenter, slot.Buffer, slot.Step, slot.WidthScale, out slot.Bounds);
            ComputeBuffer.CopyCount(slot.Buffer, farArgsBuffer, slot.ArgsOffset + sizeof(uint));
            slot.HasContent = true;
            built++;
        }
    }

    private void RenderFar()
    {
        if (far == null) return;

        for (int i = 0; i < far.Length; i++)
        {
            FarBlock slot = far[i];
            if (!slot.Active || !slot.HasContent) continue;

            Graphics.DrawMeshInstancedIndirect(
                ringMeshes[slot.Ring],
                0,
                grassMaterial,
                slot.Bounds,
                farArgsBuffer,
                slot.ArgsOffset,
                slot.Block,
                ShadowCastingMode.Off,
                false,
                gameObject.layer
            );
        }
    }

    private void ReleaseFar()
    {
        farArgsBuffer?.Release();
        farArgsBuffer = null;

        if (far != null)
        {
            for (int i = 0; i < far.Length; i++)
            {
                far[i].Buffer?.Release();
                far[i].Buffer = null;
                far[i].Block = null;
            }
        }

        far = null;
        wanted = null;
        order = null;
        orderCount = 0;
        farPoolReady = false;
        hasFarCenter = false;
        farMap = null;
    }
}
