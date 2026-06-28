using UnityEngine;

public class LegRenderer
{
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private int verticesCount;
    private int resolution;
    private int verticesPerLeg;
    private int trianglesPerLeg;
    private int maxVertices;

    public void Initialize(Mimic mimic)
    {
        verticesCount = mimic.verticeCount;
        resolution = mimic.legResolution;
        verticesPerLeg = (resolution - 1) * verticesCount + 1;
        trianglesPerLeg = (resolution - 2) * verticesCount * 6 + (verticesCount - 1) * 3;

        maxVertices = verticesPerLeg * mimic.maxLegs;
        int maxTris = trianglesPerLeg * mimic.maxLegs;

        vertices = new Vector3[maxVertices];
        triangles = new int[maxTris];

        mesh = new Mesh { name = "ProceduralLegs" };
        mesh.MarkDynamic();

        var filter = mimic.GetComponent<MeshFilter>();
        var renderer = mimic.GetComponent<MeshRenderer>();

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = mimic.legMaterial;

        PrecomputeTriangles(mimic.maxLegs);
        
        mesh.SetVertices(vertices, 0, maxVertices);
        mesh.SetTriangles(triangles, 0, maxTris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void PrecomputeTriangles(int maxLegs)
    {
        int tIdx = 0;
        for (int l = 0; l < maxLegs; l++)
        {
            int offset = l * verticesPerLeg;
            for (int i = 0; i < resolution - 2; i++)
            {
                for (int j = 0; j < verticesCount; j++)
                {
                    int next = (j + 1) % verticesCount;
                    int a = offset + i * verticesCount + j;
                    int b = offset + i * verticesCount + next;
                    int c = offset + (i + 1) * verticesCount + j;
                    int d = offset + (i + 1) * verticesCount + next;
                    
                    triangles[tIdx++] = a;
                    triangles[tIdx++] = b;
                    triangles[tIdx++] = c;
                    triangles[tIdx++] = c;
                    triangles[tIdx++] = b;
                    triangles[tIdx++] = d;
                }
            }
            int ringStart = offset + (resolution - 2) * verticesCount;
            int tip = offset + verticesPerLeg - 1;
            for (int i = 1; i < verticesCount; i++)
            {
                triangles[tIdx++] = ringStart + i - 1;
                triangles[tIdx++] = ringStart + i;
                triangles[tIdx++] = tip;
            }
        }
    }

    public void UpdateMesh(Mimic mimic)
    {
        int vIdx = 0;
        float legWidth = mimic.legWidth;

        float angleStep = 360f / verticesCount;
        float cosTheta = Mathf.Cos(angleStep * Mathf.Deg2Rad);
        float sinTheta = Mathf.Sin(angleStep * Mathf.Deg2Rad);
        float oneMinusCos = 1f - cosTheta;

        foreach (var leg in mimic.Pool.GetAll())
        {
            if (leg.state == LegState.Disabled) 
            {
                for (int i = 0; i < verticesPerLeg; i++)
                {
                    vertices[vIdx++] = Vector3.zero;
                }
                continue;
            }

            for (int i = 0; i < resolution - 1; i++)
            {
                Vector3 p1 = leg.sampleBuffer[i];
                Vector3 p2 = leg.sampleBuffer[i + 1];
                Vector3 dir = (p2 - p1).normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
                
                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * (legWidth * 0.5f);
                if (perp.sqrMagnitude < 0.001f) perp = Vector3.right * (legWidth * 0.5f);

                for (int j = 0; j < verticesCount; j++)
                {
                    vertices[vIdx++] = p1 + perp - mimic.transform.position;
                    Vector3 cross = Vector3.Cross(dir, perp);
                    float dot = Vector3.Dot(dir, perp);
                    perp = perp * cosTheta + cross * sinTheta + dir * (dot * oneMinusCos);
                }
            }
            vertices[vIdx++] =
                leg.sampleBuffer[resolution - 1]
                - mimic.transform.position;
        }

        mesh.SetVertices(vertices, 0, maxVertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}