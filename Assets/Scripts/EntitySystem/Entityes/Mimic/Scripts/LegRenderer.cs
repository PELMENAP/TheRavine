using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

public class LegRenderer
{
    private const MeshUpdateFlags VertexFlags = MeshUpdateFlags.DontRecalculateBounds;

    private static readonly Dictionary<(int legs, int samples, int ring), int[]> TriangleCache = new();

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private int[] triangles;
    private int maxLegs;
    private float boundsExtent;

    private int baseSamples;
    private int baseRing;
    private int renderSamples;
    private int renderRing;
    private int verticesPerLeg;
    private int trianglesPerLeg;

    private int activeLegs = -1;
    private int uploadedVertices;
    private bool shown = true;

    public int MaxVertices { get; private set; }
    public int Samples => renderSamples;
    public int Ring => renderRing;

    public void Initialize(Mimic mimic)
    {
        int ring = mimic.verticeCount;
        int resolution = mimic.legResolution;
        maxLegs = mimic.maxLegs;

        float quality = Mathf.Clamp01(mimic.MeshQuality);
        baseSamples = math.clamp((int)math.round(resolution * quality), 3, math.max(resolution, 3));
        baseRing = math.clamp((int)math.round(ring * quality), 3, math.max(ring, 3));

        MaxVertices = ((baseSamples - 1) * baseRing + 1) * maxLegs;
        boundsExtent = mimic.maxLegDistance + mimic.newLegRadius + mimic.legMaxHeight + mimic.legWidth;

        mesh = new Mesh { name = "ProceduralLegs" };
        mesh.MarkDynamic();

        meshFilter = mimic.GetComponent<MeshFilter>();
        meshRenderer = mimic.GetComponent<MeshRenderer>();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = mimic.legMaterial;

        activeLegs = -1;
        uploadedVertices = 0;

        renderSamples = 0;
        renderRing = 0;
        SetDetail(1f);
    }

    public void SetDetail(float scale)
    {
        int samples = math.clamp((int)math.round(baseSamples * scale), 3, math.max(baseSamples, 3));
        int ring = math.clamp((int)math.round(baseRing * scale), 3, math.max(baseRing, 3));
        if (samples == renderSamples && ring == renderRing) return;

        renderSamples = samples;
        renderRing = ring;
        verticesPerLeg = (renderSamples - 1) * renderRing + 1;
        trianglesPerLeg = (renderSamples - 2) * renderRing * 6 + (renderRing - 1) * 3;

        var key = (maxLegs, renderSamples, renderRing);
        if (!TriangleCache.TryGetValue(key, out triangles))
        {
            triangles = PrecomputeTriangles();
            TriangleCache[key] = triangles;
        }

        activeLegs = -1;
    }

    public void SetShown(bool visible)
    {
        if (shown == visible) return;
        shown = visible;
        if (meshRenderer != null) meshRenderer.enabled = visible;
    }

    public void ApplyShown()
    {
        if (meshRenderer != null) meshRenderer.enabled = shown;
    }

    public void Dispose()
    {
        if (meshFilter != null && meshFilter.sharedMesh == mesh) meshFilter.sharedMesh = null;
        if (mesh != null) UnityEngine.Object.Destroy(mesh);

        mesh = null;
        meshFilter = null;
        meshRenderer = null;
        triangles = null;
    }

    private int[] PrecomputeTriangles()
    {
        var triangles = new int[trianglesPerLeg * maxLegs];
        int tIdx = 0;
        for (int l = 0; l < maxLegs; l++)
        {
            int offset = l * verticesPerLeg;
            for (int i = 0; i < renderSamples - 2; i++)
            {
                for (int j = 0; j < renderRing; j++)
                {
                    int next = (j + 1) % renderRing;
                    int a = offset + i * renderRing + j;
                    int b = offset + i * renderRing + next;
                    int c = offset + (i + 1) * renderRing + j;
                    int d = offset + (i + 1) * renderRing + next;

                    triangles[tIdx++] = a;
                    triangles[tIdx++] = b;
                    triangles[tIdx++] = c;
                    triangles[tIdx++] = c;
                    triangles[tIdx++] = b;
                    triangles[tIdx++] = d;
                }
            }
            int ringStart = offset + (renderSamples - 2) * renderRing;
            int tip = offset + verticesPerLeg - 1;
            for (int i = 1; i < renderRing; i++)
            {
                triangles[tIdx++] = ringStart + i - 1;
                triangles[tIdx++] = ringStart + i;
                triangles[tIdx++] = tip;
            }
        }

        return triangles;
    }

    public void Upload(LegStore store, int vertexCount, int legs)
    {
        if (mesh == null) return;

        var vertices = store.VertexView;
        var normals = store.NormalView;

        bool structural = legs != activeLegs;
        activeLegs = legs;

        if (!structural)
        {
            mesh.SetVertices(vertices, 0, vertexCount, VertexFlags);
            mesh.SetNormals(normals, 0, vertexCount, VertexFlags);
            return;
        }

        int indexCount = legs * trianglesPerLeg;
        if (vertexCount >= uploadedVertices)
        {
            mesh.SetVertices(vertices, 0, vertexCount, VertexFlags);
            mesh.SetIndices(triangles, 0, indexCount, MeshTopology.Triangles, 0, false);
        }
        else
        {
            mesh.SetIndices(triangles, 0, indexCount, MeshTopology.Triangles, 0, false);
            mesh.SetVertices(vertices, 0, vertexCount, VertexFlags);
        }
        mesh.SetNormals(normals, 0, vertexCount, VertexFlags);
        uploadedVertices = vertexCount;

        mesh.bounds = legs > 0 ? new Bounds(Vector3.zero, Vector3.one * (2f * boundsExtent)) : default;
    }
}
