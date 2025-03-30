using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

using TheRavine.Extensions;



namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessLiquids : IEndless
        {
            private readonly MapGenerator generator;
            private readonly int scale = MapGenerator.scale, generationSize = MapGenerator.generationSize;
            private const int chunkScale = MapGenerator.chunkScale, chunkCount = 2 * chunkScale + 1,  mapChunkSize = MapGenerator.mapChunkSize;
            private const ushort countOfQuads = mapChunkSize * chunkCount;
            public EndlessLiquids(MapGenerator _generator)
            {
                generator = _generator;
            }
            private bool[,] grid = new bool[countOfQuads, countOfQuads];
            private bool[,] visited = new bool[countOfQuads, countOfQuads];
            private List<Edge> verticalEdges = new List<Edge>(), horizontalEdges = new List<Edge>();
            private List<Vector2Int> polygon = new List<Vector2Int>();
            public void UpdateChunk(Vector2Int Vposition)
            {
                Array.Clear(visited, 0, visited.Length);
                polygon.Clear();
                verticalEdges.Clear();
                horizontalEdges.Clear();
                
                for (int xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                {
                    for (int yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                    {
                        int[,] curChunkHeightMap = generator.GetMapData(new Vector2Int(Vposition.x + xOffset, Vposition.y + yOffset)).heightMap;
                        for (int x = 0; x < mapChunkSize; x++)
                            for (int y = 0; y < mapChunkSize; y++)
                                grid[(xOffset + chunkScale) * mapChunkSize + x, (yOffset + chunkScale) * mapChunkSize + y] = curChunkHeightMap[x, y] <= MapGenerator.waterLevel;
                    }
                }
                GenerateMesh();
                generator.waterT.position = new((Vposition.x - 1) * generationSize, (Vposition.y - 1) * generationSize);
            }
            private void GenerateMesh()
            {
                ExtractPolygons();
                ExtractHoleSegments();

                DebugHelper.DrawPoints(polygon, Color.green, 5f, 0.1f);
                foreach (var hole in verticalEdges)
                    DebugHelper.DrawSegment(hole.P1, hole.P2, Color.red);   
                foreach (var hole in horizontalEdges)
                    DebugHelper.DrawSegment(hole.P1, hole.P2, Color.blue);   
                Debug.Log(polygon.Count + "  " + verticalEdges.Count + "  " + horizontalEdges.Count);

                BowyerWatsonTriangulation bowyerWatsonTriangulation = new();
                Mesh mesh = bowyerWatsonTriangulation.GenerateMesh(polygon, verticalEdges, horizontalEdges);

                generator.waterF.mesh = mesh;
            }

            private void ExtractHoleSegments()
            {   
                Vector2Int[,] holeStarts = new Vector2Int[2, countOfQuads];
                bool[] inProcess = new bool[countOfQuads];
                
                for (int i = 0; i < 2; i++) 
                {
                    for (int primary = 4; primary < countOfQuads - 4; primary += 4)
                    {
                        inProcess[primary] = false;
                        
                        for (int secondary = 1; secondary + 1 < countOfQuads; secondary++)
                        {
                            int x = i == 0 ? primary : secondary;
                            int y = i == 0 ? secondary : primary;
                            
                            bool isHole = !grid[x, y] && !grid[x + (i == 0 ? 0 : 1), y + (i == 0 ? 1 : 0)] && !grid[x - (i == 0 ? 0 : 1), y - (i == 0 ? 1 : 0)];
                            
                            if (isHole && !inProcess[primary])
                            {
                                holeStarts[i, primary] = new Vector2Int(x, y);
                                inProcess[primary] = true;
                            }
                            else if (inProcess[primary] && !isHole)
                            {
                                Vector2Int start = holeStarts[i, primary];
                                Vector2Int end = new Vector2Int(i == 0 ? x : secondary - 1, i == 0 ? secondary - 1 : y);
                                
                                (i == 0 ? verticalEdges : horizontalEdges).Add(new Edge(start * scale, end * scale));
                                inProcess[primary] = false;
                            }
                        }
                        
                        if (inProcess[primary])
                        {
                            Vector2Int start = holeStarts[i, primary];
                            Vector2Int end = new Vector2Int(i == 0 ? primary : countOfQuads, i == 0 ? countOfQuads : primary);
                            (i == 0 ? verticalEdges : horizontalEdges).Add(new Edge(start * scale, end * scale));
                        }
                    }
                }
            }

            private void ExtractPolygons() // search the true field
            {
                for (int x = 1; x < countOfQuads; x+=4)
                {
                    for (int y = 1; y < countOfQuads; y+=4)
                    {
                        if (grid[x, y] && !visited[x, y])
                        {
                            FloodFill(x, y);
                        }
                    }
                }
            }
            private int tooFarToRender = 2;
            private void FloodFill(int x, int y) // need cache the defaults Vectior2Int
            {
                Stack<Vector2Int> stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(x, y));
                
                while (stack.Count > 0)
                {
                    Vector2Int point = stack.Pop();
                    if (point.x - tooFarToRender < 0 || point.x + tooFarToRender >= countOfQuads || point.y - tooFarToRender < 0 || point.y + tooFarToRender >= countOfQuads || visited[point.x, point.y] || !grid[point.x, point.y])
                        continue;

                    visited[point.x, point.y] = true;
                    
                    if(IsBorderExtra(point.x, point.y))
                        polygon.Add(point * scale);

                    stack.Push(new Vector2Int(point.x + 1, point.y));
                    stack.Push(new Vector2Int(point.x - 1, point.y));
                    stack.Push(new Vector2Int(point.x, point.y + 1));
                    stack.Push(new Vector2Int(point.x, point.y - 1));
                }
            }


            private bool IsBorderExtra(int x, int y)
            {
                int countOfNeighbours = 0;
                countOfNeighbours += grid[x + 1, y] ? 0 : 1;
                countOfNeighbours += grid[x - 1, y] ? 0 : 1;
                countOfNeighbours += grid[x + 1, y + 1] ? 0 : 1;
                countOfNeighbours += grid[x - 1, y - 1] ? 0 : 1;
                countOfNeighbours += grid[x + 1, y - 1] ? 0 : 1;
                countOfNeighbours += grid[x - 1, y + 1] ? 0 : 1;
                countOfNeighbours += grid[x, y + 1] ? 0 : 1;
                countOfNeighbours += grid[x, y - 1] ? 0 : 1;
                // return countOfNeighbours == 1 || countOfNeighbours > 3;
                return countOfNeighbours > 0;
            }
        }










        public class BowyerWatsonTriangulation
        {
            public Mesh GenerateMesh(List<Vector2Int> points, List<Edge> verticalEdges, List<Edge> horizontalEdges)
            {
                List<Triangle> triangles = Triangulate(points, verticalEdges, horizontalEdges);

                int pointCount = points.Count;
                int triangleCount = triangles.Count;

                Vector3[] vertices = new Vector3[pointCount];
                Vector2[] uvs = new Vector2[pointCount];
                int[] indices = new int[triangleCount * 3];
                Dictionary<Vector2Int, int> vertexIndexMap = new Dictionary<Vector2Int, int>(pointCount);

                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

                foreach (var p in points)
                {
                    if (p.x < minX) minX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y > maxY) maxY = p.y;
                }

                int index = 0;
                foreach (var tri in triangles)
                {
                    indices[index++] = GetVertexIndex(tri.A, vertices, uvs, vertexIndexMap, minX, minY, maxX, maxY);
                    indices[index++] = GetVertexIndex(tri.B, vertices, uvs, vertexIndexMap, minX, minY, maxX, maxY);
                    indices[index++] = GetVertexIndex(tri.C, vertices, uvs, vertexIndexMap, minX, minY, maxX, maxY);
                }

                Mesh mesh = new Mesh
                {
                    vertices = vertices,
                    triangles = indices,
                    uv = uvs
                };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                return mesh;
            }

            private int GetVertexIndex(Vector2Int point, Vector3[] vertices, Vector2[] uvs, Dictionary<Vector2Int, int> vertexIndexMap,
                                    float minX, float minY, float maxX, float maxY)
            {
                if (!vertexIndexMap.TryGetValue(point, out int index))
                {
                    index = vertexIndexMap.Count;
                    vertices[index] = new Vector3(point.x, point.y, 0);
                    uvs[index] = new Vector2((point.x - minX) / (maxX - minX), (point.y - minY) / (maxY - minY));
                    vertexIndexMap[point] = index;
                }
                return index;
            }

            private List<Triangle> Triangulate(List<Vector2Int> points, List<Edge> verticalEdges, List<Edge> horizontalEdges)
            {
                List<Triangle> resultTriangles = new List<Triangle>(points.Count * 2);
                Triangle superTriangle = CreateSuperTriangle(points);
                resultTriangles.Add(superTriangle);

                List<Triangle> badTriangles = new List<Triangle>();
                List<Edge> polygon = new List<Edge>();

                foreach (var point in points)
                {
                    badTriangles.Clear();
                    polygon.Clear();

                    for (int i = resultTriangles.Count - 1; i >= 0; i--)
                    {
                        if (resultTriangles[i].CircumcircleContains(point))
                        {
                            badTriangles.Add(resultTriangles[i]);
                            resultTriangles.RemoveAt(i);
                        }
                    }

                    foreach (var tri in badTriangles)
                    {
                        polygon.Add(new Edge(tri.A, tri.B));
                        polygon.Add(new Edge(tri.B, tri.C));
                        polygon.Add(new Edge(tri.C, tri.A));
                    }

                    RemoveDuplicateEdges(polygon);

                    foreach (var edge in polygon)
                        resultTriangles.Add(new Triangle(edge.P1, edge.P2, point));
                }

                resultTriangles.RemoveAll(t => t.HasVertex(superTriangle.A) ||
                                            t.HasVertex(superTriangle.B) ||
                                            t.HasVertex(superTriangle.C) ||
                                            IsInsideHole(t, verticalEdges, horizontalEdges));

                return resultTriangles;
            }

            private Triangle CreateSuperTriangle(List<Vector2Int> points)
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var p in points)
                {
                    if (p.x < minX) minX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y > maxY) maxY = p.y;
                }

                int deltaMax = Mathf.Max(maxX - minX, maxY - minY) * 2;
                return new Triangle(new Vector2Int(minX - deltaMax, minY - deltaMax),
                                    new Vector2Int(maxX + deltaMax, minY - deltaMax),
                                    new Vector2Int((minX + maxX) / 2, maxY + deltaMax));
            }

            private bool IsInsideHole(Triangle t, List<Edge> verticalEdges, List<Edge> horizontalEdges) // lists of edges already sorted (maybe using SortedList?)
            {
                int minX = Mathf.Min(t.A.x, t.B.x, t.C.x);
                int maxX = Mathf.Max(t.A.x, t.B.x, t.C.x);
                int minY = Mathf.Min(t.A.y, t.B.y, t.C.y);
                int maxY = Mathf.Max(t.A.y, t.B.y, t.C.y);

                int vStart = verticalEdges.BinarySearch(new Edge(new Vector2Int(minX, 0), new Vector2Int(minX, 0)), EdgeXComparer.Instance);
                int vEnd = verticalEdges.BinarySearch(new Edge(new Vector2Int(maxX, 0), new Vector2Int(maxX, 0)), EdgeXComparer.Instance);
                
                vStart = vStart < 0 ? ~vStart : vStart;
                vEnd = vEnd < 0 ? ~vEnd : vEnd;

                for (int i = vStart; i < vEnd && i < verticalEdges.Count; i++)
                {
                    if (IntersectTE.IsIntersecting(t, verticalEdges[i]))
                        return true;
                }

                int hStart = horizontalEdges.BinarySearch(new Edge(new Vector2Int(0, minY), new Vector2Int(0, minY)), EdgeYComparer.Instance);
                int hEnd = horizontalEdges.BinarySearch(new Edge(new Vector2Int(0, maxY), new Vector2Int(0, maxY)), EdgeYComparer.Instance);
                
                hStart = hStart < 0 ? ~hStart : hStart;
                hEnd = hEnd < 0 ? ~hEnd : hEnd;

                for (int i = hStart; i < hEnd && i < horizontalEdges.Count; i++)
                {
                    if (IntersectTE.IsIntersecting(t, horizontalEdges[i]))
                        return true;
                }

                return false;
            }

            private static void RemoveDuplicateEdges(List<Edge> edges)
            {
                var uniqueEdges = new HashSet<Edge>();
                var duplicates = new List<Edge>();
                
                foreach (var edge in edges)
                {
                    if (!uniqueEdges.Add(edge))
                        duplicates.Add(edge);
                }
                
                foreach (var duplicate in duplicates)
                {
                    uniqueEdges.Remove(duplicate);
                }
                
                edges.Clear();
                edges.AddRange(uniqueEdges);
            }
        }




        public class Triangle
        {
            public Vector2Int A, B, C;
            private Vector2 center;
            private float radiusSquared;

            public Triangle(Vector2Int a, Vector2Int b, Vector2Int c)
            {
                A = a;
                B = b;
                C = c;
                if (!CalculateCircumcircle())
                {
                    throw new ArgumentException("Degenerate triangle: points are collinear.");
                }
            }

            public Vector2 GetCentroid()
            {
                return new Vector2((A.x + B.x + C.x) / 3f, (A.y + B.y + C.y) / 3f);
            }

            private bool CalculateCircumcircle()
            {
                float ax = A.x, ay = A.y;
                float bx = B.x, by = B.y;
                float cx = C.x, cy = C.y;

                float d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
                if (Mathf.Abs(d) < 1e-6) return false;

                float ux = ((ax * ax + ay * ay) * (by - cy) +
                            (bx * bx + by * by) * (cy - ay) +
                            (cx * cx + cy * cy) * (ay - by)) / d;

                float uy = ((ax * ax + ay * ay) * (cx - bx) +
                            (bx * bx + by * by) * (ax - cx) +
                            (cx * cx + cy * cy) * (bx - ax)) / d;

                center = new Vector2(ux, uy);
                radiusSquared = (center - (Vector2)A).sqrMagnitude;
                return true;
            }

            public bool CircumcircleContains(Vector2Int p)
            {
                return (center - (Vector2)p).sqrMagnitude < radiusSquared;
            }

            public bool HasVertex(Vector2Int p)
            {
                return p == A || p == B || p == C;
            }
        }


        public class Edge
        {
            public Vector2Int P1, P2;
            
            private readonly int hashCode;
            
            public Edge(Vector2Int p1, Vector2Int p2)
            {
                if (Compare(p1, p2) < 0)
                {
                    P1 = p1;
                    P2 = p2;
                }
                else
                {
                    P1 = p2;
                    P2 = p1;
                }
                hashCode = P1.GetHashCode() ^ (P2.GetHashCode() * 397);
            }
            private static int Compare(Vector2Int a, Vector2Int b)
            {
                if (a.x != b.x) return a.x.CompareTo(b.x);
                return a.y.CompareTo(b.y);
            }
            
            public override bool Equals(object obj)
            {
                if (!(obj is Edge other)) return false;
                return P1.Equals(other.P1) && P2.Equals(other.P2);
            }
            
            public override int GetHashCode()
            {
                return hashCode;
            }
        }

        class EdgeXComparer : IComparer<Edge>
        {
            public static readonly EdgeXComparer Instance = new EdgeXComparer();
            public int Compare(Edge a, Edge b) => a.P1.x.CompareTo(b.P1.x);
        }

        class EdgeYComparer : IComparer<Edge>
        {
            public static readonly EdgeYComparer Instance = new EdgeYComparer();
            public int Compare(Edge a, Edge b) => a.P1.y.CompareTo(b.P1.y);
        }


        public static class IntersectTE
        {
            public static bool IsIntersecting(Triangle triangle, Edge edge)
            {
                if (DoEdgesIntersect(edge.P1, edge.P2, triangle.A, triangle.B) ||
                    DoEdgesIntersect(edge.P1, edge.P2, triangle.B, triangle.C) ||
                    DoEdgesIntersect(edge.P1, edge.P2, triangle.C, triangle.A))
                {
                    return true;
                }

                return false;
            }
            private static float Cross(Vector2Int a, Vector2Int b, Vector2Int c)
            {
                return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            }

            private static bool DoEdgesIntersect(Vector2Int p1, Vector2Int p2, Vector2Int p3, Vector2Int p4)
            {
                float d1 = Cross(p3, p4, p1);
                float d2 = Cross(p3, p4, p2);
                float d3 = Cross(p1, p2, p3);
                float d4 = Cross(p1, p2, p4);

                if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                    return true;

                if (Mathf.Abs(d1) < 1e-6f && OnSegment(p3, p4, p1)) return true;
                if (Mathf.Abs(d2) < 1e-6f && OnSegment(p3, p4, p2)) return true;
                if (Mathf.Abs(d3) < 1e-6f && OnSegment(p1, p2, p3)) return true;
                if (Mathf.Abs(d4) < 1e-6f && OnSegment(p1, p2, p4)) return true;

                return false;
            }
            private static bool OnSegment(Vector2Int p1, Vector2Int p2, Vector2Int p)
            {
                return p.x >= Mathf.Min(p1.x, p2.x) && p.x <= Mathf.Max(p1.x, p2.x) &&
                    p.y >= Mathf.Min(p1.y, p2.y) && p.y <= Mathf.Max(p1.y, p2.y);
            }
        }
    }
}
