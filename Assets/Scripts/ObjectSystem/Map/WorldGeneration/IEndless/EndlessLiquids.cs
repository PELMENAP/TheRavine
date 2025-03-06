using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;



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
            private int[,] curChunkHeightMap;
            public void UpdateChunk(Vector2Int Vposition)
            {
                for (int xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                {
                    for (int yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                    {
                        curChunkHeightMap = generator.GetMapData(new Vector2Int(Vposition.x + xOffset, Vposition.y + yOffset)).heightMap;
                        for (int x = 0; x < mapChunkSize; x++)
                            for (int y = 0; y < mapChunkSize; y++)
                                grid[(xOffset + chunkScale) * mapChunkSize + x, (yOffset + chunkScale) * mapChunkSize + y] = curChunkHeightMap[x, y] <= MapGenerator.waterLevel;
                    }
                }
                GenerateMesh();
                generator.waterT.position = new((Vposition.x - 1) * generationSize, (Vposition.y - 1) * generationSize);
            }
            private bool[,] visited = new bool[countOfQuads, countOfQuads];
            private float maxTriangleEdgeSize = 10000f;
            private void GenerateMesh()
            {
                visited = new bool[countOfQuads, countOfQuads];
                List<Vector2Int> polygon = ExtractPolygons(true);
                List<Vector2Int> holes = ExtractPolygons(false);

                Debug.Log(polygon.Count + "  " + holes.Count);

                Mesh mesh = BowyerWatsonTriangulation.GenerateMesh(polygon, holes, maxTriangleEdgeSize);

                generator.waterF.mesh = mesh;
            }
            private List<Vector2Int> ExtractPolygons(bool isPolygon)
            {
                List<Vector2Int> polygon = new List<Vector2Int>();

                for (int x = 1; x < countOfQuads; x+=2)
                {
                    for (int y = 1; y < countOfQuads; y+=2)
                    {
                        if ((grid[x, y] == isPolygon) && !visited[x, y])
                        {
                            FloodFill(x, y, polygon, isPolygon);
                        }
                    }
                }
                return polygon;
            }

            private int farDistance = 2;
 
            private void FloodFill(int x, int y, List<Vector2Int> polygon, bool isPolygon)
            {
                Stack<Vector2Int> stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(x, y));
                
                while (stack.Count > 0)
                {
                    Vector2Int point = stack.Pop();
                    if (point.x - farDistance < 0 || point.x + farDistance >= countOfQuads || point.y - farDistance < 0 || point.y + farDistance >= countOfQuads || visited[point.x, point.y] || grid[point.x, point.y] != isPolygon)
                        continue;

                    visited[point.x, point.y] = true;

                    // if (!isPolygon)
                    //     if ((point.x + 1 < countOfQuads && grid[point.x + 1, point.y]) ||
                    //         (point.x - 1 >= 0 && grid[point.x - 1, point.y]) ||
                    //         (point.y + 1 < countOfQuads && grid[point.x, point.y + 1]) ||
                    //         (point.y - 1 >= 0 && grid[point.x, point.y - 1]))
                    //             continue;
                    
                    if(isPolygon)
                    {
                        if(IsBorder(point, !isPolygon))
                            polygon.Add(point * scale);
                    }
                    else
                        if(IsNotSoFar(point))
                            polygon.Add(point * scale);

                    stack.Push(new Vector2Int(point.x + 1, point.y));
                    stack.Push(new Vector2Int(point.x - 1, point.y));
                    stack.Push(new Vector2Int(point.x, point.y + 1));
                    stack.Push(new Vector2Int(point.x, point.y - 1));
                }
            }


            private bool IsBorder(Vector2Int point, bool isPolygon)
            {
                return ((grid[point.x + 1, point.y] == isPolygon) ||
                    (grid[point.x - 1, point.y] == isPolygon) ||
                    (grid[point.x, point.y + 1] == isPolygon) ||
                    (grid[point.x, point.y - 1] == isPolygon));
            }

            private bool IsNotSoFar(Vector2Int point)
            {
                return (grid[point.x + farDistance, point.y]) ||
                        (grid[point.x - farDistance, point.y]) ||
                        (grid[point.x, point.y + farDistance]) ||
                        (grid[point.x, point.y - farDistance]);
            }
        }

        public static class BowyerWatsonTriangulation
        {
            public static Mesh GenerateMesh(List<Vector2Int> points, List<Vector2Int> holes, float maxTriangleEdgeSize)
            {
                var triangles = Triangulate(points, holes, maxTriangleEdgeSize);
                int pointCount = points.Count;
                int triangleCount = triangles.Count;
                
                Vector3[] vertices = new Vector3[pointCount];
                Vector2[] uvs = new Vector2[pointCount];
                int[] indices = new int[triangleCount * 3];
                
                var vertexIndexMap = new Dictionary<Vector2Int, int>(pointCount);
                
                float minX = points[0].x, minY = points[0].y, 
                    maxX = points[0].x, maxY = points[0].y;
                
                for (int i = 1; i < points.Count; i++)
                {
                    var p = points[i];
                    minX = Mathf.Min(minX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x);
                    maxY = Mathf.Max(maxY, p.y);
                }
                
                int index = 0;
                foreach (var tri in triangles)
                {
                    indices[index++] = GetVertexIndex(tri.A, vertices, uvs, vertexIndexMap, minX, minY, maxX, maxY);
                    indices[index++] = GetVertexIndex(tri.B, vertices, uvs, vertexIndexMap, minX, minY, maxX, maxY);
                    indices[index++] = GetVertexIndex(tri.C, vertices, uvs, vertexIndexMap, minX, minY, maxX, maxY);
                }
                
                var mesh = new Mesh
                {
                    vertices = vertices,
                    triangles = indices,
                    uv = uvs
                };
                
                return mesh;
            }
            
            private static int GetVertexIndex(Vector2Int point, Vector3[] vertices, Vector2[] uvs, 
                Dictionary<Vector2Int, int> vertexIndexMap, float minX, float minY, float maxX, float maxY)
            {
                if (!vertexIndexMap.TryGetValue(point, out int index))
                {
                    index = vertexIndexMap.Count;
                    vertices[index] = new Vector3(point.x, point.y, 0);
                    uvs[index] = new Vector2(
                        (point.x - minX) / (maxX - minX), 
                        (point.y - minY) / (maxY - minY)
                    );
                    vertexIndexMap[point] = index;
                }
                return index;
            }
            
            private static List<Triangle> Triangulate(List<Vector2Int> points, List<Vector2Int> holes, float maxTriangleEdgeSize)
            {
                var resultTriangles = new List<Triangle>(points.Count * 2);
                var superTriangle = CreateSuperTriangle(points);
                resultTriangles.Add(superTriangle);
                
                var badTriangles = new List<Triangle>();
                var polygon = new List<Edge>();
                
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
                    
                    for (int i = 0; i < badTriangles.Count; i++)
                    {
                        var tri = badTriangles[i];
                        polygon.Add(new Edge(tri.A, tri.B));
                        polygon.Add(new Edge(tri.B, tri.C));
                        polygon.Add(new Edge(tri.C, tri.A));
                    }
                    
                    RemoveDuplicateEdges(polygon);
                    
                    foreach (var edge in polygon)
                        resultTriangles.Add(new Triangle(edge.P1, edge.P2, point));
                }
                
                resultTriangles.RemoveAll(t => 
                    t.HasVertex(superTriangle.A) || 
                    t.HasVertex(superTriangle.B) || 
                    t.HasVertex(superTriangle.C) || 
                    IsInsideHole(t, holes) || 
                    t.TooLargeEdge(maxTriangleEdgeSize)
                );
                
                return resultTriangles;
            }
            
            private static Triangle CreateSuperTriangle(List<Vector2Int> points)
            {
                int minX = points[0].x, minY = points[0].y, 
                    maxX = points[0].x, maxY = points[0].y;
                
                for (int i = 1; i < points.Count; i++)
                {
                    var p = points[i];
                    minX = Mathf.Min(minX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x);
                    maxY = Mathf.Max(maxY, p.y);
                }
                
                int deltaMax = Mathf.Max(maxX - minX, maxY - minY) * 2;
                
                return new Triangle(
                    new Vector2Int(minX - deltaMax, minY - deltaMax),
                    new Vector2Int(maxX + deltaMax, minY - deltaMax),
                    new Vector2Int((minX + maxX) / 2, maxY + deltaMax)
                );
            }
            
            private static bool IsInsideHole(Triangle t, List<Vector2Int> holes)
            {
                for (int i = 0; i < holes.Count; i++)
                {
                    if (t.ContainsPoint(holes[i])) return true;
                }
                return false;
            }
            
            private static void RemoveDuplicateEdges(List<Edge> edges)
            {
                for (int i = edges.Count - 1; i >= 0; i--)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (edges[i].Equals(edges[j]))
                        {
                            edges.RemoveAt(i);
                            edges.RemoveAt(j);
                            i--;
                            break;
                        }
                    }
                }
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

            public bool ContainsPoint(Vector2Int p)
            {
                float factor = (B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y);
                if (Mathf.Abs(factor) < 1e-6) return false;

                bool IsInside(Vector2 point)
                {
                    float w1 = ((B.y - C.y) * (point.x - C.x) + (C.x - B.x) * (point.y - C.y)) / factor;
                    float w2 = ((C.y - A.y) * (point.x - C.x) + (A.x - C.x) * (point.y - C.y)) / factor;
                    float w3 = 1 - w1 - w2;
                    return w1 >= 0 && w2 >= 0 && w3 >= 0;
                }

                if (IsInside(p)) return true;
                float offset = 0.25f;
                return IsInside(new Vector2(p.x + offset, p.y)) ||
                    IsInside(new Vector2(p.x - offset, p.y)) ||
                    IsInside(new Vector2(p.x, p.y + offset)) ||
                    IsInside(new Vector2(p.x, p.y - offset));
            }

            public bool TooLargeEdge(float maxTriangleEdgeSize)
            {
                float maxEdgeSizeSquared = maxTriangleEdgeSize * maxTriangleEdgeSize;
                return (A - B).sqrMagnitude > maxEdgeSizeSquared ||
                    (C - B).sqrMagnitude > maxEdgeSizeSquared ||
                    (A - C).sqrMagnitude > maxEdgeSizeSquared;
            }
        }


        public class Edge
        {
            public Vector2Int P1, P2;

            public Edge(Vector2Int p1, Vector2Int p2)
            {
                P1 = p1;
                P2 = p2;
            }

            public override bool Equals(object obj)
            {
                if (obj is Edge other)
                {
                    return (P1 == other.P1 && P2 == other.P2) || (P1 == other.P2 && P2 == other.P1);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return P1.GetHashCode() ^ P2.GetHashCode();
            }
        }
        
    }
}
