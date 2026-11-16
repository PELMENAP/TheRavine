using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

using TheRavine.Extensions;

namespace TheRavine.Generator.EndlessGenerators
{
    public sealed class RavineBackdrop : IEndless
    {
        public const string ShaderName = "The Ravine/Terrain/RavineBackdrop";

        private static readonly int SideId = Shader.PropertyToID("_BackdropSide");
        private static readonly int ReachId = Shader.PropertyToID("_BackdropReach");
        private static readonly int HazeId = Shader.PropertyToID("_BackdropHaze");
        private static readonly int NoiseHeightId = Shader.PropertyToID("_NoiseHeight");

        private readonly Mesh mesh;
        private readonly Material material;
        private readonly bool ownsMaterial;
        private readonly RenderParams[] sides = new RenderParams[2];
        private readonly MaterialPropertyBlock[] props = new MaterialPropertyBlock[2];
        private readonly float cell;
        private readonly float reachX;
        private readonly float terrainReach;
        private readonly Vector3 boundsSize;
        private readonly float boundsMidY;

        private Matrix4x4 matrix = Matrix4x4.identity;
        private float originX = float.NaN;
        private float centerX;
        private bool hasCenter;

        public RavineBackdrop(MapGenerator generator, ChunkGenerationSettings settings, Material material, bool ownsMaterial)
        {
            this.material = material;
            this.ownsMaterial = ownsMaterial;

            RavineProfile ravine = generator.Ravine;
            cell = math.max(settings.backdropCell, 8f);
            reachX = math.max(settings.backdropReach, cell);
            float wallRun = math.rcp(ravine.InvRun);
            float rim = ravine.HalfWidth + wallRun;
            float end = rim + math.max(settings.backdropPlateauDepth, 0f);

            mesh = BuildMesh(cell, reachX, ravine.HalfWidth, wallRun, math.max(settings.backdropWallRows, 1), end);
            terrainReach = (generator.LodRingCount * MapGenerator.WindowSide + MapGenerator.chunkScale + 0.5f) * MapGenerator.chunkSize;

            float noise = material.HasProperty(NoiseHeightId) ? math.abs(material.GetFloat(NoiseHeightId)) : 0f;
            float low = -noise - MapGenerator.maxTerrainHeight;
            float high = ravine.Height + noise + MapGenerator.maxTerrainHeight;
            boundsMidY = (low + high) * 0.5f;
            boundsSize = new Vector3(2f * reachX + 2f * cell, high - low, 2f * end);

            for (int s = 0; s < sides.Length; s++)
            {
                var block = new MaterialPropertyBlock();
                block.SetFloat(SideId, s == 0 ? 1f : -1f);
                block.SetFloat(HazeId, math.max(settings.backdropHazeDensity, 0f));
                block.SetVector(ReachId, Vector4.zero);
                props[s] = block;
                sides[s] = new RenderParams(material)
                {
                    matProps = block,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
            }
        }

        private static Mesh BuildMesh(float cell, float reachX, float halfWidth, float wallRun, int wallRows, float end)
        {
            var rows = new List<float>(256);
            for (float v = 0f; v < halfWidth; v += cell) rows.Add(v);
            for (int k = 0; k <= wallRows; k++) rows.Add(halfWidth + wallRun * k / wallRows);
            float rim = halfWidth + wallRun;
            for (float v = rim + cell; v < end; v += cell) rows.Add(v);
            if (rows[rows.Count - 1] < end) rows.Add(end);

            int columns = (int)math.ceil(2f * reachX / cell) + 1;
            int rowCount = rows.Count;
            var vertices = new Vector3[columns * rowCount];
            for (int r = 0; r < rowCount; r++)
            for (int c = 0; c < columns; c++)
                vertices[r * columns + c] = new Vector3(-reachX + c * cell, 0f, rows[r]);

            var indices = new int[(columns - 1) * (rowCount - 1) * 6];
            int w = 0;
            for (int r = 0; r < rowCount - 1; r++)
            for (int c = 0; c < columns - 1; c++)
            {
                int a = r * columns + c;
                int b = a + 1;
                int d = a + columns;
                int e = d + 1;
                indices[w++] = a; indices[w++] = d; indices[w++] = e;
                indices[w++] = a; indices[w++] = e; indices[w++] = b;
            }

            var mesh = new Mesh { name = "RavineBackdrop" };
            mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f * reachX, 1e5f, 2f * end));
            mesh.UploadMeshData(true);
            return mesh;
        }

        public void UpdateChunk(long center)
        {
            hasCenter = true;
            centerX = (Position2Int.GetX(center) + 0.5f) * MapGenerator.chunkSize;
            var reach = new Vector4(centerX, (Position2Int.GetY(center) + 0.5f) * MapGenerator.chunkSize, terrainReach, 0f);
            for (int s = 0; s < props.Length; s++) props[s].SetVector(ReachId, reach);
        }

        public void Tick(long deadline)
        {
            if (!hasCenter) return;

            Camera camera = Camera.main;
            float x = camera != null ? camera.transform.position.x : centerX;
            float snapped = math.floor(x / cell) * cell;
            if (snapped != originX)
            {
                originX = snapped;
                matrix = Matrix4x4.Translate(new Vector3(snapped, 0f, 0f));
                var bounds = new Bounds(new Vector3(snapped, boundsMidY, 0f), boundsSize);
                for (int s = 0; s < sides.Length; s++) sides[s].worldBounds = bounds;
            }

            for (int s = 0; s < sides.Length; s++)
                Graphics.RenderMesh(in sides[s], mesh, 0, matrix);
        }

        public void OnChunkDirty(long chunkKey) { }

        public void Dispose()
        {
            if (mesh != null) Object.Destroy(mesh);
            if (ownsMaterial && material != null) Object.Destroy(material);
        }
    }
}
