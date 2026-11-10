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
    private Vector3[] vertices;
    private Vector3[] normals;
    private float3[] points;
    private int[] triangles;
    private int resolution;
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

    public void Initialize(Mimic mimic)
    {
        int ring = mimic.verticeCount;
        resolution = mimic.legResolution;
        maxLegs = mimic.maxLegs;

        float quality = Mathf.Clamp01(mimic.MeshQuality);
        baseSamples = math.clamp((int)math.round(resolution * quality), 3, math.max(resolution, 3));
        baseRing = math.clamp((int)math.round(ring * quality), 3, math.max(ring, 3));

        int maxVertices = ((baseSamples - 1) * baseRing + 1) * maxLegs;
        vertices = new Vector3[maxVertices];
        normals = new Vector3[maxVertices];
        points = new float3[baseSamples];
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
        vertices = null;
        normals = null;
        points = null;
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

    private static float3 Bezier(float3[] h, float t)
    {
        float u = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float u4 = u3 * u;
        float u5 = u4 * u;
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        float t5 = t4 * t;

        return h[0] * (u5 * u) +
               h[1] * (6f * t * u5) +
               h[2] * (15f * t2 * u4) +
               h[3] * (20f * t3 * u3) +
               h[4] * (15f * t4 * u2) +
               h[5] * (6f * t5 * u) +
               h[6] * (t5 * t);
    }

    public void UpdateMesh(Mimic mimic)
    {
        int vIdx = 0;
        int legs = 0;
        float halfWidth = mimic.legWidth * 0.5f;
        float invHalfWidth = halfWidth > 1e-6f ? 1f / halfWidth : 0f;
        int last = resolution - 1;
        float invLast = 1f / last;
        int segments = renderSamples - 1;

        math.sincos(2f * math.PI / renderRing, out float sinTheta, out float cosTheta);
        float oneMinusCos = 1f - cosTheta;

        float3 mimicPosition = mimic.transform.position;
        var all = mimic.Pool.GetAll();

        for (int l = 0; l < all.Count; l++)
        {
            var leg = all[l];
            if (leg.state == LegState.Disabled) continue;
            legs++;

            float3[] handles = leg.handles;
            float progression = leg.progression;
            bool grown = progression >= 0.0001f;
            for (int k = 0; k <= segments; k++)
            {
                float t = grown ? (k * last / segments) * invLast * progression : 0f;
                points[k] = Bezier(handles, t);
            }

            float3 dir = new float3(0f, 1f, 0f);
            for (int k = 0; k < segments; k++)
            {
                float3 p1 = points[k];
                float3 d = points[k + 1] - p1;
                float dl2 = math.lengthsq(d);
                dir = dl2 > 1e-10f ? d * math.rsqrt(dl2) : float3.zero;
                if (math.lengthsq(dir) < 0.001f) dir = new float3(0f, 1f, 0f);

                float3 side = math.cross(dir, new float3(0f, 1f, 0f));
                float sl2 = math.lengthsq(side);
                float3 perp = sl2 > 1e-10f ? side * (math.rsqrt(sl2) * halfWidth) : float3.zero;
                if (math.lengthsq(perp) < 0.001f) perp = new float3(halfWidth, 0f, 0f);

                float3 center = p1 - mimicPosition;
                for (int j = 0; j < renderRing; j++)
                {
                    vertices[vIdx] = center + perp;
                    normals[vIdx] = perp * invHalfWidth;
                    vIdx++;

                    float3 cross = math.cross(dir, perp);
                    float dot = math.dot(dir, perp);
                    perp = perp * cosTheta + cross * sinTheta + dir * (dot * oneMinusCos);
                }
            }
            vertices[vIdx] = points[segments] - mimicPosition;
            normals[vIdx] = dir;
            vIdx++;
        }

        bool structural = legs != activeLegs;
        activeLegs = legs;

        if (!structural)
        {
            mesh.SetVertices(vertices, 0, vIdx, VertexFlags);
            mesh.SetNormals(normals, 0, vIdx, VertexFlags);
            return;
        }

        int indexCount = legs * trianglesPerLeg;
        if (vIdx >= uploadedVertices)
        {
            mesh.SetVertices(vertices, 0, vIdx, VertexFlags);
            mesh.SetIndices(triangles, 0, indexCount, MeshTopology.Triangles, 0, false);
        }
        else
        {
            mesh.SetIndices(triangles, 0, indexCount, MeshTopology.Triangles, 0, false);
            mesh.SetVertices(vertices, 0, vIdx, VertexFlags);
        }
        mesh.SetNormals(normals, 0, vIdx, VertexFlags);
        uploadedVertices = vIdx;

        mesh.bounds = legs > 0 ? new Bounds(Vector3.zero, Vector3.one * (2f * boundsExtent)) : default;
    }
}
