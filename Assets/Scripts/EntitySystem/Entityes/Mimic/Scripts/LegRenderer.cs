using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

public class LegRenderer
{
    private const MeshUpdateFlags VertexFlags = MeshUpdateFlags.DontRecalculateBounds;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private int baseVertexCount;
    private int resolution;
    private int maxLegs;
    private float boundsExtent;

    private int lod = -1;
    private int lodStride;
    private int lodSamples;
    private int lodRing;
    private int lodVerticesPerLeg;
    private int lodTrianglesPerLeg;
    private bool lodNormalsPerFrame;

    private int activeLegs = -1;
    private int uploadedVertices;

    public void Initialize(Mimic mimic)
    {
        baseVertexCount = mimic.verticeCount;
        resolution = mimic.legResolution;
        maxLegs = mimic.maxLegs;

        int verticesPerLeg = (resolution - 1) * baseVertexCount + 1;
        int trianglesPerLeg = (resolution - 2) * baseVertexCount * 6 + (baseVertexCount - 1) * 3;

        vertices = new Vector3[verticesPerLeg * maxLegs];
        triangles = new int[trianglesPerLeg * maxLegs];
        boundsExtent = mimic.maxLegDistance + mimic.newLegRadius + mimic.legMaxHeight + mimic.legWidth;

        mesh = new Mesh { name = "ProceduralLegs" };
        mesh.MarkDynamic();

        var filter = mimic.GetComponent<MeshFilter>();
        var renderer = mimic.GetComponent<MeshRenderer>();

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = mimic.legMaterial;

        lod = -1;
        activeLegs = -1;
        uploadedVertices = 0;
    }

    private void ApplyLod(Mimic mimic)
    {
        lod = mimic.Lod;
        lodStride = math.max(1, mimic.LodStride);
        lodSamples = (resolution - 2) / lodStride + 2;
        lodRing = math.min(baseVertexCount, math.max(3, mimic.LodRingVertices));
        lodVerticesPerLeg = (lodSamples - 1) * lodRing + 1;
        lodTrianglesPerLeg = (lodSamples - 2) * lodRing * 6 + (lodRing - 1) * 3;
        lodNormalsPerFrame = mimic.LodNormalsPerFrame;
        PrecomputeTriangles();
    }

    private void PrecomputeTriangles()
    {
        int tIdx = 0;
        for (int l = 0; l < maxLegs; l++)
        {
            int offset = l * lodVerticesPerLeg;
            for (int i = 0; i < lodSamples - 2; i++)
            {
                for (int j = 0; j < lodRing; j++)
                {
                    int next = (j + 1) % lodRing;
                    int a = offset + i * lodRing + j;
                    int b = offset + i * lodRing + next;
                    int c = offset + (i + 1) * lodRing + j;
                    int d = offset + (i + 1) * lodRing + next;

                    triangles[tIdx++] = a;
                    triangles[tIdx++] = b;
                    triangles[tIdx++] = c;
                    triangles[tIdx++] = c;
                    triangles[tIdx++] = b;
                    triangles[tIdx++] = d;
                }
            }
            int ringStart = offset + (lodSamples - 2) * lodRing;
            int tip = offset + lodVerticesPerLeg - 1;
            for (int i = 1; i < lodRing; i++)
            {
                triangles[tIdx++] = ringStart + i - 1;
                triangles[tIdx++] = ringStart + i;
                triangles[tIdx++] = tip;
            }
        }
    }

    public void UpdateMesh(Mimic mimic)
    {
        bool structural = false;
        if (mimic.Lod != lod)
        {
            ApplyLod(mimic);
            structural = true;
        }

        int vIdx = 0;
        int legs = 0;
        float legWidth = mimic.legWidth;
        int last = resolution - 1;

        math.sincos(2f * math.PI / lodRing, out float sinTheta, out float cosTheta);
        float oneMinusCos = 1f - cosTheta;

        Vector3 mimicPosition = mimic.transform.position;
        var all = mimic.Pool.GetAll();

        for (int l = 0; l < all.Count; l++)
        {
            var leg = all[l];
            if (leg.state == LegState.Disabled) continue;
            legs++;

            Vector3[] samples = leg.sampleBuffer;
            for (int k = 0; k < lodSamples - 1; k++)
            {
                Vector3 p1 = samples[math.min(k * lodStride, last)];
                Vector3 p2 = samples[math.min((k + 1) * lodStride, last)];
                Vector3 dir = (p2 - p1).normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;

                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * (legWidth * 0.5f);
                if (perp.sqrMagnitude < 0.001f) perp = Vector3.right * (legWidth * 0.5f);

                for (int j = 0; j < lodRing; j++)
                {
                    vertices[vIdx++] = p1 + perp - mimicPosition;
                    Vector3 cross = Vector3.Cross(dir, perp);
                    float dot = Vector3.Dot(dir, perp);
                    perp = perp * cosTheta + cross * sinTheta + dir * (dot * oneMinusCos);
                }
            }
            vertices[vIdx++] = samples[last] - mimicPosition;
        }

        if (legs != activeLegs)
        {
            activeLegs = legs;
            structural = true;
        }

        if (!structural)
        {
            mesh.SetVertices(vertices, 0, vIdx, VertexFlags);
            if (lodNormalsPerFrame) mesh.RecalculateNormals();
            return;
        }

        int indexCount = legs * lodTrianglesPerLeg;
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
        uploadedVertices = vIdx;

        mesh.bounds = legs > 0 ? new Bounds(Vector3.zero, Vector3.one * (2f * boundsExtent)) : default;
        mesh.RecalculateNormals();
    }
}
