using System;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct BoidGpu
{
    public Vector3 position;
    public float phase;
    public Vector3 velocity;
    public float bank;
}

namespace TheRavine.Generator
{
    [Serializable]
    public struct BoidFlockWeights
    {
        public float separationRadius;
        public float separationMin;
        public int maxNeighbors;
        public float separation;
        public float alignment;
        public float cohesion;
        public float bounds;
        public float target;
        public float noise;
        public Vector2 speedRange;
        public float maxSteer;
    }

    public sealed class BoidFlockGpu : IDisposable
    {
        private const int ThreadGroup = 64;
        private const int ScanBlock = 1024;
        private const int MaxScanLevels = 4;

        private static readonly int BoidsInId = Shader.PropertyToID("boidsIn");
        private static readonly int BoidsOutId = Shader.PropertyToID("boidsOut");
        private static readonly int CellStartId = Shader.PropertyToID("cellStart");
        private static readonly int BoidKeyId = Shader.PropertyToID("boidKey");
        private static readonly int ScanDataId = Shader.PropertyToID("scanData");
        private static readonly int ScanSumsId = Shader.PropertyToID("scanSums");
        private static readonly int ScanCountId = Shader.PropertyToID("scanCount");
        private static readonly int BoidCountId = Shader.PropertyToID("boidCount");
        private static readonly int CellTotalId = Shader.PropertyToID("cellTotal");
        private static readonly int GridDimsId = Shader.PropertyToID("gridDims");
        private static readonly int ExtentsId = Shader.PropertyToID("boundsExtents");
        private static readonly int CellInvId = Shader.PropertyToID("cellInv");
        private static readonly int Perception2Id = Shader.PropertyToID("perception2");
        private static readonly int MaxNeighborsId = Shader.PropertyToID("maxNeighbors");
        private static readonly int Separation2Id = Shader.PropertyToID("separation2");
        private static readonly int SepMin2Id = Shader.PropertyToID("sepMin2");
        private static readonly int WSepId = Shader.PropertyToID("wSep");
        private static readonly int WAliId = Shader.PropertyToID("wAli");
        private static readonly int WCohId = Shader.PropertyToID("wCoh");
        private static readonly int WBoundsId = Shader.PropertyToID("wBounds");
        private static readonly int WTargetId = Shader.PropertyToID("wTarget");
        private static readonly int WNoiseId = Shader.PropertyToID("wNoise");
        private static readonly int MinSpeedId = Shader.PropertyToID("minSpeed");
        private static readonly int MaxSpeedId = Shader.PropertyToID("maxSpeed");
        private static readonly int MaxSteerId = Shader.PropertyToID("maxSteer");
        private static readonly int DtId = Shader.PropertyToID("dt");
        private static readonly int TimeId = Shader.PropertyToID("time");
        private static readonly int FrameId = Shader.PropertyToID("frame");
        private static readonly int TargetId = Shader.PropertyToID("target");
        private static readonly int WindVelocityId = Shader.PropertyToID("windVelocity");
        private static readonly int CursorParamsId = Shader.PropertyToID("cursorParams");
        private static readonly int ShockParamsId = Shader.PropertyToID("shockParams");

        private readonly ComputeShader compute;
        private readonly GraphicsBuffer[] boids = new GraphicsBuffer[2];
        private readonly GraphicsBuffer[] scanLevels = new GraphicsBuffer[MaxScanLevels + 1];
        private readonly int[] scanCounts = new int[MaxScanLevels + 1];
        private readonly int[] gridDims;
        private readonly Vector3 extents;
        private readonly float perception;
        private readonly int count;
        private readonly int cellTotal;
        private readonly int boidGroups;
        private readonly int cellGroups;
        private readonly int kernelClear;
        private readonly int kernelCount;
        private readonly int kernelScan;
        private readonly int kernelAdd;
        private readonly int kernelScatter;
        private readonly int kernelStep;
        private GraphicsBuffer cellStartBuffer;
        private GraphicsBuffer boidKeyBuffer;
        private int scanLevelCount;

        public int Count => count;
        public GraphicsBuffer Output => boids[0];

        public BoidFlockGpu(ComputeShader compute, int count, Vector3 extents, float perception)
        {
            this.compute = compute;
            this.count = count;
            this.extents = extents;
            this.perception = perception;

            kernelClear = compute.FindKernel("ClearGrid");
            kernelCount = compute.FindKernel("CountGrid");
            kernelScan = compute.FindKernel("Scan");
            kernelAdd = compute.FindKernel("AddOffsets");
            kernelScatter = compute.FindKernel("Scatter");
            kernelStep = compute.FindKernel("Step");

            gridDims = new[]
            {
                Mathf.Max(1, Mathf.CeilToInt(extents.x * 2f / perception)),
                Mathf.Max(1, Mathf.CeilToInt(extents.y * 2f / perception)),
                Mathf.Max(1, Mathf.CeilToInt(extents.z * 2f / perception))
            };
            cellTotal = gridDims[0] * gridDims[1] * gridDims[2];
            boidGroups = Mathf.CeilToInt(count / (float)ThreadGroup);
            cellGroups = Mathf.CeilToInt((cellTotal + 1) / (float)ThreadGroup);

            int stride = Marshal.SizeOf<BoidGpu>();
            boids[0] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
            boids[1] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
            cellStartBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cellTotal + 1, sizeof(uint));
            boidKeyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(uint) * 2);
            BuildScanLevels(cellTotal + 1);
        }

        private void BuildScanLevels(int n)
        {
            scanLevels[0] = cellStartBuffer;
            scanCounts[0] = n;
            scanLevelCount = 1;

            while (true)
            {
                int blocks = (scanCounts[scanLevelCount - 1] + ScanBlock - 1) / ScanBlock;
                scanLevels[scanLevelCount] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, blocks, sizeof(uint));
                scanCounts[scanLevelCount] = blocks;
                if (blocks <= 1 || scanLevelCount == MaxScanLevels) break;
                scanLevelCount++;
            }
        }

        public void SetData(BoidGpu[] data)
        {
            boids[0].SetData(data);
            boids[1].SetData(data);
        }

        public static void ApplyWeights(ComputeShader compute, in BoidFlockWeights w)
        {
            compute.SetInt(MaxNeighborsId, w.maxNeighbors);
            compute.SetFloat(Separation2Id, w.separationRadius * w.separationRadius);
            compute.SetFloat(SepMin2Id, w.separationMin * w.separationMin);
            compute.SetFloat(WSepId, w.separation);
            compute.SetFloat(WAliId, w.alignment);
            compute.SetFloat(WCohId, w.cohesion);
            compute.SetFloat(WBoundsId, w.bounds);
            compute.SetFloat(WTargetId, w.target);
            compute.SetFloat(WNoiseId, w.noise);
            compute.SetFloat(MinSpeedId, w.speedRange.x);
            compute.SetFloat(MaxSpeedId, w.speedRange.y);
            compute.SetFloat(MaxSteerId, w.maxSteer);
        }

        public static void ApplyFrame(ComputeShader compute, float dt, float time, int frame, Vector3 target, Vector3 windVelocity)
        {
            compute.SetFloat(DtId, dt);
            compute.SetFloat(TimeId, time);
            compute.SetInt(FrameId, frame);
            compute.SetVector(TargetId, target);
            compute.SetVector(WindVelocityId, windVelocity);
        }

        public static void ClearInteraction(ComputeShader compute)
        {
            compute.SetVector(CursorParamsId, Vector4.zero);
            compute.SetVector(ShockParamsId, Vector4.zero);
        }

        public void Dispatch()
        {
            compute.SetInt(BoidCountId, count);
            compute.SetInt(CellTotalId, cellTotal);
            compute.SetInts(GridDimsId, gridDims);
            compute.SetVector(ExtentsId, extents);
            compute.SetFloat(CellInvId, 1f / perception);
            compute.SetFloat(Perception2Id, perception * perception);

            compute.SetBuffer(kernelClear, CellStartId, cellStartBuffer);
            compute.SetBuffer(kernelCount, CellStartId, cellStartBuffer);
            compute.SetBuffer(kernelCount, BoidKeyId, boidKeyBuffer);
            compute.SetBuffer(kernelScatter, CellStartId, cellStartBuffer);
            compute.SetBuffer(kernelScatter, BoidKeyId, boidKeyBuffer);
            compute.SetBuffer(kernelStep, CellStartId, cellStartBuffer);

            compute.SetBuffer(kernelCount, BoidsInId, boids[0]);
            compute.SetBuffer(kernelScatter, BoidsInId, boids[0]);
            compute.SetBuffer(kernelScatter, BoidsOutId, boids[1]);
            compute.SetBuffer(kernelStep, BoidsInId, boids[1]);
            compute.SetBuffer(kernelStep, BoidsOutId, boids[0]);

            compute.Dispatch(kernelClear, cellGroups, 1, 1);
            compute.Dispatch(kernelCount, boidGroups, 1, 1);
            DispatchScan();
            compute.Dispatch(kernelScatter, boidGroups, 1, 1);
            compute.Dispatch(kernelStep, boidGroups, 1, 1);
        }

        private void DispatchScan()
        {
            for (int l = 0; l < scanLevelCount; l++)
            {
                compute.SetBuffer(kernelScan, ScanDataId, scanLevels[l]);
                compute.SetBuffer(kernelScan, ScanSumsId, scanLevels[l + 1]);
                compute.SetInt(ScanCountId, scanCounts[l]);
                compute.Dispatch(kernelScan, scanCounts[l + 1], 1, 1);
            }

            for (int l = scanLevelCount - 2; l >= 0; l--)
            {
                compute.SetBuffer(kernelAdd, ScanDataId, scanLevels[l]);
                compute.SetBuffer(kernelAdd, ScanSumsId, scanLevels[l + 1]);
                compute.SetInt(ScanCountId, scanCounts[l]);
                compute.Dispatch(kernelAdd, scanCounts[l + 1], 1, 1);
            }
        }

        public static Mesh BuildMesh()
        {
            var m = new Mesh { name = "BoidMesh" };
            m.vertices = new[]
            {
                new Vector3(0f, 0f, 0.6f),
                new Vector3(0f, 0f, -0.6f),
                new Vector3(-0.7f, 0f, -0.35f),
                new Vector3(0.7f, 0f, -0.35f),
                new Vector3(0f, 0.18f, -0.15f)
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f)
            };
            m.triangles = new[] { 0, 2, 1, 0, 1, 3, 0, 1, 4 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }

        public void Dispose()
        {
            boids[0]?.Release();
            boids[1]?.Release();
            cellStartBuffer?.Release();
            boidKeyBuffer?.Release();
            for (int i = 1; i < scanLevels.Length; i++)
            {
                scanLevels[i]?.Release();
                scanLevels[i] = null;
            }
            scanLevels[0] = null;
            boids[0] = null;
            boids[1] = null;
            cellStartBuffer = null;
            boidKeyBuffer = null;
        }
    }
}
