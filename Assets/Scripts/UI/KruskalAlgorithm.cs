using System.Collections.Generic;
using UnityEngine;

public class KruskalAlgorithm
{
    private struct Edge
    {
        public int src, dest;
        public float weight;
        public Edge(int src, int dest, float weight)
        {
            this.src = src;
            this.dest = dest;
            this.weight = weight;
        }
    }

    private struct Subset
    {
        public int parent, rank;
    }

    private int Find(Subset[] subsets, int i)
    {
        if (subsets[i].parent != i)
            subsets[i].parent = Find(subsets, subsets[i].parent);
        return subsets[i].parent;
    }

    private void Union(Subset[] subsets, int x, int y)
    {
        int xroot = Find(subsets, x);
        int yroot = Find(subsets, y);

        if (subsets[xroot].rank < subsets[yroot].rank)
            subsets[xroot].parent = yroot;
        else if (subsets[xroot].rank > subsets[yroot].rank)
            subsets[yroot].parent = xroot;
        else
        {
            subsets[yroot].parent = xroot;
            subsets[xroot].rank++;
        }
    }

    public void KruskalMST(Vector2[] points)
    {
        List<Edge> edges = new List<Edge>();
        Subset[] subsets = new Subset[points.Length];

        for (int v = 0; v < points.Length; ++v)
            subsets[v] = new Subset { parent = v, rank = 0 };

        for (int i = 0; i < points.Length - 1; ++i)
        {
            for (int j = i + 1; j < points.Length; ++j)
            {
                float weight = Vector2.Distance(points[i], points[j]);
                edges.Add(new Edge(i, j, weight));
            }
        }
        edges.Sort((a, b) => a.weight.CompareTo(b.weight));
        List<Edge> result = new List<Edge>();
        int e = 0;
        int k = 0;
        while (e < points.Length - 1 && k < edges.Count)
        {
            Edge next_edge = edges[k++];
            int x = Find(subsets, next_edge.src);
            int y = Find(subsets, next_edge.dest);
            if (x != y)
            {
                result.Add(next_edge);
                Union(subsets, x, y);
                e++;
            }
        }
        foreach (Edge edge in result)
            Debug.Log($"Edge {edge.src} - {edge.dest} weight: {edge.weight}");
    }

    // void Start()
    // {
    //     Vector2[] points = new Vector2[] {
    //         new Vector2(1, 2),
    //         new Vector2(4, 2),
    //         new Vector2(4, -2),
    //         new Vector2(1, -3),
    //         new Vector2(-1, 3),
    //         new Vector2(-1, -1),
    //         new Vector2(4, 0)
    //     };

    //     KruskalMST(points);
    // }
}
