using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator.EndlessGenerators
{
    public sealed class FarObjectRenderer : IEndless, IViewDirectional
    {
        private const int RingStride = ObjectInfo.MaxVisibleRings + 1;
        private const int BillboardSlot = int.MinValue;

        private static readonly int InstancesId = Shader.PropertyToID("_FarInstances");
        private static readonly int InstanceOffsetId = Shader.PropertyToID("_InstanceOffset");
        private static readonly int BillboardBoxId = Shader.PropertyToID("_BillboardBox");
        private static readonly int BillboardCellId = Shader.PropertyToID("_BillboardCell");

        private struct ChunkSource
        {
            [NativeDisableUnsafePtrRestriction] public unsafe ObjectInstInfo* Objects;
            public int Count;
            public int Ring;
        }

        private struct TypeInfo
        {
            public int BatchBase;
            public int VariantCount;
            public int SlotsPerVariant;
        }

        private sealed class Command
        {
            public Mesh Mesh;
            public int Batch;
            public RenderParams Params;
            public uint IndexStart;
            public uint IndexCount;
            public int BaseVertex;
        }

        private readonly MapGenerator generator;
        private readonly ChunkGenerationSettings settings;
        private readonly Command[] commands;
        private readonly MaterialPropertyBlock[] batchProps;
        private readonly int batchCount;
        private readonly int capacity;
        private readonly Mesh billboardQuad;

        private NativeArray<int> denseById;
        private NativeArray<TypeInfo> types;
        private NativeArray<int> slots;
        private NativeArray<int> counts;
        private NativeArray<int> offsets;
        private NativeArray<int> cursors;
        private NativeArray<float4> instances;
        private NativeArray<ChunkSource> sources;
        private NativeReference<int> total;
        private NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> args;
        private GraphicsBuffer instanceBuffer;
        private GraphicsBuffer commandBuffer;

        private long center;
        private int facing;
        private bool hasCenter;
        private bool dirty;
        private int framesSinceBuild;

        public FarObjectRenderer(MapGenerator generator, ObjectSystem objectSystem, ChunkGenerationSettings settings)
        {
            this.generator = generator;
            this.settings = settings;
            facing = generator.Facing;
            capacity = math.max(1, settings.farMaxInstances);

            ObjectInfoRegistry registry = objectSystem.infoRegistry;
            int typeCount = registry.Count;
            int rings = math.min(generator.LodRingCount, ObjectInfo.MaxVisibleRings);

            denseById = new NativeArray<int>(math.max(registry.IdCapacity, 1), Allocator.Persistent);
            for (int id = 0; id < denseById.Length; id++)
                denseById[id] = registry.DenseIndexOf(id);

            types = new NativeArray<TypeInfo>(math.max(typeCount, 1), Allocator.Persistent);
            slots = new NativeArray<int>(math.max(typeCount, 1) * RingStride, Allocator.Persistent);
            for (int i = 0; i < slots.Length; i++) slots[i] = -1;

            billboardQuad = CreateQuad();

            var commandList = new List<Command>();
            var propList = new List<MaterialPropertyBlock>();
            var slotKeys = new List<int>();
            var slotRings = new List<int>();
            int batch = 0;

            for (int d = 0; d < typeCount; d++)
            {
                ObjectInfo info = registry.GetDense(d);
                ObjectLodSet set = info.LodSet;
                int visible = math.min(info.VisibleRings, rings);
                if (set == null || info.FarMode == FarMode.Hidden || visible <= 0 || set.VariantCount <= 0) continue;

                slotKeys.Clear();
                slotRings.Clear();

                for (int r = 1; r <= visible; r++)
                {
                    bool billboard = info.FarMode == FarMode.MeshThenBillboard && set.UsesBillboard(r);
                    int key = billboard ? BillboardSlot : set.GetMeshLod(0, r);
                    int slot = slotKeys.IndexOf(key);
                    if (slot < 0)
                    {
                        slot = slotKeys.Count;
                        slotKeys.Add(key);
                        slotRings.Add(r);
                    }
                    slots[d * RingStride + r] = slot;
                }

                types[d] = new TypeInfo
                {
                    BatchBase = batch,
                    VariantCount = set.VariantCount,
                    SlotsPerVariant = slotKeys.Count
                };

                ShadowCastingMode shadows = info.CastShadowsFar ? ShadowCastingMode.On : ShadowCastingMode.Off;

                for (int v = 0; v < set.VariantCount; v++)
                {
                    for (int s = 0; s < slotKeys.Count; s++, batch++)
                    {
                        var props = new MaterialPropertyBlock();
                        propList.Add(props);

                        if (slotKeys[s] == BillboardSlot)
                        {
                            props.SetVector(BillboardBoxId, set.GetBillboardBox(v));
                            props.SetFloat(BillboardCellId, set.GetBillboardCell(v));
                            commandList.Add(new Command
                            {
                                Mesh = billboardQuad,
                                Batch = batch,
                                Params = CreateParams(set.BillboardMaterial, props, shadows),
                                IndexStart = billboardQuad.GetIndexStart(0),
                                IndexCount = billboardQuad.GetIndexCount(0),
                                BaseVertex = (int)billboardQuad.GetBaseVertex(0)
                            });
                            continue;
                        }

                        Mesh mesh = set.GetMesh(v, slotRings[s]);
                        if (mesh == null) continue;

                        for (int sub = 0; sub < set.SubmeshCount && sub < mesh.subMeshCount; sub++)
                        {
                            Material material = set.GetMaterial(slotRings[s], sub);
                            if (material == null) continue;

                            set.GetIndexRange(v, slotKeys[s], sub, out uint start, out uint count);
                            commandList.Add(new Command
                            {
                                Mesh = mesh,
                                Batch = batch,
                                Params = CreateParams(material, props, shadows),
                                IndexStart = start,
                                IndexCount = count,
                                BaseVertex = (int)mesh.GetBaseVertex(sub)
                            });
                        }
                    }
                }
            }

            batchCount = batch;
            commands = commandList.ToArray();
            batchProps = propList.ToArray();

            counts = new NativeArray<int>(math.max(batchCount, 1), Allocator.Persistent);
            offsets = new NativeArray<int>(math.max(batchCount, 1), Allocator.Persistent);
            cursors = new NativeArray<int>(math.max(batchCount, 1), Allocator.Persistent);
            instances = new NativeArray<float4>(capacity, Allocator.Persistent);
            total = new NativeReference<int>(0, Allocator.Persistent);

            int sourceCapacity = 0;
            for (int r = 1; r <= rings; r++)
                sourceCapacity += (2 * r + 1) * MapGenerator.WindowSide * MapGenerator.WindowSide;
            sources = new NativeArray<ChunkSource>(math.max(sourceCapacity, 1), Allocator.Persistent);

            args = new NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs>(math.max(commands.Length, 1), Allocator.Persistent);
            for (int i = 0; i < commands.Length; i++)
                args[i] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = commands[i].IndexCount,
                    startIndex = commands[i].IndexStart,
                    baseVertexIndex = (uint)commands[i].BaseVertex
                };

            instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 4);
            commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, args.Length, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            commandBuffer.SetData(args);
            Shader.SetGlobalBuffer(InstancesId, instanceBuffer);
        }

        private static RenderParams CreateParams(Material material, MaterialPropertyBlock props, ShadowCastingMode shadows) =>
            new(material)
            {
                matProps = props,
                shadowCastingMode = shadows,
                receiveShadows = true
            };

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "FarBillboardQuad" };
            mesh.SetVertices(new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f)
            });
            mesh.SetIndices(new[] { 0, 2, 1, 1, 2, 3 }, MeshTopology.Triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            return mesh;
        }

        public void UpdateChunk(long newCenter)
        {
            center = newCenter;
            hasCenter = true;
            dirty = true;
        }

        public void SetFacing(int newFacing)
        {
            if (newFacing == facing) return;
            facing = newFacing;
            dirty = true;
        }

        public void OnChunkDirty(long chunkKey) { }

        public void Tick(long deadline)
        {
            if (!hasCenter || batchCount == 0) return;

            framesSinceBuild++;
            if (dirty)
            {
                int missing = CollectSources(out int sourceCount);
                if (missing == 0 || framesSinceBuild >= settings.farRebuildIntervalFrames)
                {
                    Rebuild(sourceCount);
                    dirty = missing > 0;
                }
            }

            Render();
        }

        private unsafe int CollectSources(out int sourceCount)
        {
            int n = 0;
            int missing = 0;
            int rings = math.min(generator.LodRingCount, ObjectInfo.MaxVisibleRings);

            for (int ring = 1; ring <= rings; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    long block = Position2Int.Offset(center, dx * MapGenerator.WindowSide, ring * facing * MapGenerator.WindowSide);

                    for (int gz = 0; gz < MapGenerator.WindowSide; gz++)
                    for (int gx = 0; gx < MapGenerator.WindowSide; gx++)
                    {
                        long key = MapGenerator.WindowChunk(block, gx, gz);
                        if (!generator.TryGetChunk(key, out ChunkData cd) || cd.DetailLevel > ring)
                        {
                            missing++;
                            continue;
                        }

                        sources[n++] = new ChunkSource
                        {
                            Objects = (ObjectInstInfo*)cd.Objects.GetUnsafeReadOnlyPtr(),
                            Count = cd.Objects.Length,
                            Ring = ring
                        };
                    }
                }
            }

            sourceCount = n;
            return missing;
        }

        private void Rebuild(int sourceCount)
        {
            framesSinceBuild = 0;

            new CollectJob
            {
                Sources = sources,
                SourceCount = sourceCount,
                DenseById = denseById,
                Types = types,
                Slots = slots,
                Counts = counts,
                Offsets = offsets,
                Cursors = cursors,
                Instances = instances,
                Total = total,
                BatchCount = batchCount
            }.Run();

            int written = total.Value;
            if (written > 0)
                instanceBuffer.SetData(instances, 0, 0, written);

            for (int b = 0; b < batchCount; b++)
                batchProps[b].SetFloat(InstanceOffsetId, offsets[b]);

            float reach = (generator.LodRingCount * MapGenerator.WindowSide + 2) * MapGenerator.chunkSize;
            float drop = CurvedWorld.MaxDrop(reach);
            Vector3 origin = MapGenerator.WindowOriginWorld(center);
            Vector3 mid = origin + new Vector3(MapGenerator.generationSize * 0.5f, 0f, MapGenerator.generationSize * 0.5f);
            var bounds = new Bounds(
                mid + new Vector3(0f, (MapGenerator.maxTerrainHeight - drop) * 0.5f, 0f),
                new Vector3(reach * 2f, MapGenerator.maxTerrainHeight * 4f + drop, reach * 2f));

            for (int i = 0; i < commands.Length; i++)
            {
                Command c = commands[i];
                c.Params.worldBounds = bounds;

                var a = args[i];
                a.instanceCount = (uint)counts[c.Batch];
                a.startInstance = 0;
                args[i] = a;
            }

            commandBuffer.SetData(args);
        }

        private void Render()
        {
            for (int i = 0; i < commands.Length; i++)
            {
                if (args[i].instanceCount == 0) continue;
                Command c = commands[i];
                Graphics.RenderMeshIndirect(in c.Params, c.Mesh, commandBuffer, 1, i);
            }
        }

        public void Dispose()
        {
            instanceBuffer?.Release();
            commandBuffer?.Release();
            instanceBuffer = null;
            commandBuffer = null;

            if (denseById.IsCreated) denseById.Dispose();
            if (types.IsCreated) types.Dispose();
            if (slots.IsCreated) slots.Dispose();
            if (counts.IsCreated) counts.Dispose();
            if (offsets.IsCreated) offsets.Dispose();
            if (cursors.IsCreated) cursors.Dispose();
            if (instances.IsCreated) instances.Dispose();
            if (sources.IsCreated) sources.Dispose();
            if (total.IsCreated) total.Dispose();
            if (args.IsCreated) args.Dispose();

            if (billboardQuad != null) Object.Destroy(billboardQuad);
        }

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        private unsafe struct CollectJob : IJob
        {
            [ReadOnly] public NativeArray<ChunkSource> Sources;
            public int SourceCount;
            [ReadOnly] public NativeArray<int> DenseById;
            [ReadOnly] public NativeArray<TypeInfo> Types;
            [ReadOnly] public NativeArray<int> Slots;
            public NativeArray<int> Counts;
            public NativeArray<int> Offsets;
            public NativeArray<int> Cursors;
            [WriteOnly] public NativeArray<float4> Instances;
            public NativeReference<int> Total;
            public int BatchCount;

            private int BatchOf(in ObjectInstInfo info, int ring)
            {
                int id = info.PrefabID;
                if ((uint)id >= (uint)DenseById.Length) return -1;

                int dense = DenseById[id];
                if (dense < 0) return -1;

                int slot = Slots[dense * RingStride + ring];
                if (slot < 0) return -1;

                TypeInfo t = Types[dense];
                float3 p = info.Position;
                int seed = (int)(math.hash(p) & 0x7FFFFFFFu);
                return t.BatchBase + (seed % t.VariantCount) * t.SlotsPerVariant + slot;
            }

            public void Execute()
            {
                for (int b = 0; b < BatchCount; b++) Counts[b] = 0;

                for (int s = 0; s < SourceCount; s++)
                {
                    ChunkSource src = Sources[s];
                    for (int i = 0; i < src.Count; i++)
                    {
                        int b = BatchOf(src.Objects[i], src.Ring);
                        if (b >= 0) Counts[b]++;
                    }
                }

                int capacity = Instances.Length;
                int running = 0;
                for (int b = 0; b < BatchCount; b++)
                {
                    int count = math.min(Counts[b], capacity - running);
                    Counts[b] = count;
                    Offsets[b] = running;
                    Cursors[b] = running;
                    running += count;
                }

                for (int s = 0; s < SourceCount; s++)
                {
                    ChunkSource src = Sources[s];
                    for (int i = 0; i < src.Count; i++)
                    {
                        ObjectInstInfo info = src.Objects[i];
                        int b = BatchOf(info, src.Ring);
                        if (b < 0) continue;

                        int cursor = Cursors[b];
                        if (cursor >= Offsets[b] + Counts[b]) continue;

                        Instances[cursor] = new float4(info.Position.x, info.Position.y, info.Position.z, 1f);
                        Cursors[b] = cursor + 1;
                    }
                }

                Total.Value = running;
            }
        }
    }
}
