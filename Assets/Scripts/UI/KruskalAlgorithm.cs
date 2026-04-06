using System.Collections.Generic;
using UnityEngine;

public class KruskalAlgorithm
{
    public struct Edge
    {
        public int src, dest;
        public float sqrWeight;

        public Edge(int src, int dest, float sqrWeight)
        {
            this.src = src;
            this.dest = dest;
            this.sqrWeight = sqrWeight;
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

    public List<Edge> GetMST(Vector2[] points)
    {
        int pointsCount = points.Length;
        if (pointsCount == 0) return new List<Edge>();

        int expectedEdgesCount = pointsCount * (pointsCount - 1) / 2;
        List<Edge> edges = new List<Edge>(expectedEdgesCount);
        Subset[] subsets = new Subset[pointsCount];

        for (int v = 0; v < pointsCount; ++v)
            subsets[v] = new Subset { parent = v, rank = 0 };

        for (int i = 0; i < pointsCount - 1; ++i)
        {
            for (int j = i + 1; j < pointsCount; ++j)
            {
                float sqrWeight = (points[i] - points[j]).sqrMagnitude;
                edges.Add(new Edge(i, j, sqrWeight));
            }
        }

        edges.Sort((a, b) => a.sqrWeight.CompareTo(b.sqrWeight));
        
        List<Edge> result = new List<Edge>(pointsCount - 1);
        int e = 0;
        int k = 0;
        
        while (e < pointsCount - 1 && k < edges.Count)
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
        
        return result;
    }
}