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

    private int renderSamples;
    private int renderRing;
    private int verticesPerLeg;
    private int trianglesPerLeg;

    private int activeLegs = -1;
    private int uploadedVertices;

    public void Initialize(Mimic mimic)
    {
        baseVertexCount = mimic.verticeCount;
        resolution = mimic.legResolution;
        maxLegs = mimic.maxLegs;

        float quality = Mathf.Clamp01(mimic.MeshQuality);
        renderSamples = math.clamp((int)math.round(resolution * quality), 3, math.max(resolution, 3));
        renderRing = math.clamp((int)math.round(baseVertexCount * quality), 3, math.max(baseVertexCount, 3));
        verticesPerLeg = (renderSamples - 1) * renderRing + 1;
        trianglesPerLeg = (renderSamples - 2) * renderRing * 6 + (renderRing - 1) * 3;

        vertices = new Vector3[verticesPerLeg * maxLegs];
        triangles = new int[trianglesPerLeg * maxLegs];
        boundsExtent = mimic.maxLegDistance + mimic.newLegRadius + mimic.legMaxHeight + mimic.legWidth;

        mesh = new Mesh { name = "ProceduralLegs" };
        mesh.MarkDynamic();

        var filter = mimic.GetComponent<MeshFilter>();
        var renderer = mimic.GetComponent<MeshRenderer>();

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = mimic.legMaterial;

        activeLegs = -1;
        uploadedVertices = 0;
        PrecomputeTriangles();
    }

    private void PrecomputeTriangles()
    {
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
    }

    public void UpdateMesh(Mimic mimic)
    {
        bool structural = false;

        int vIdx = 0;
        int legs = 0;
        float legWidth = mimic.legWidth;
        int last = resolution - 1;

        math.sincos(2f * math.PI / renderRing, out float sinTheta, out float cosTheta);
        float oneMinusCos = 1f - cosTheta;

        Vector3 mimicPosition = mimic.transform.position;
        var all = mimic.Pool.GetAll();

        for (int l = 0; l < all.Count; l++)
        {
            var leg = all[l];
            if (leg.state == LegState.Disabled) continue;
            legs++;

            Vector3[] samples = leg.sampleBuffer;
            for (int k = 0; k < renderSamples - 1; k++)
            {
                Vector3 p1 = samples[k * last / (renderSamples - 1)];
                Vector3 p2 = samples[(k + 1) * last / (renderSamples - 1)];
                Vector3 dir = (p2 - p1).normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;

                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * (legWidth * 0.5f);
                if (perp.sqrMagnitude < 0.001f) perp = Vector3.right * (legWidth * 0.5f);

                for (int j = 0; j < renderRing; j++)
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
            mesh.RecalculateNormals();
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
        uploadedVertices = vIdx;

        mesh.bounds = legs > 0 ? new Bounds(Vector3.zero, Vector3.one * (2f * boundsExtent)) : default;
        mesh.RecalculateNormals();
    }
}
