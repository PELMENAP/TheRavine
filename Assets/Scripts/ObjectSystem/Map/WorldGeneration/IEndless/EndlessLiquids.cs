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
            private readonly byte scale = MapGenerator.scale, generationSize = MapGenerator.generationSize;
            private const byte chunkScale = MapGenerator.chunkScale, chunkCount = 2 * chunkScale + 1,  mapChunkSize = MapGenerator.mapChunkSize;
            private const ushort countOfQuads = mapChunkSize * chunkCount;
            public EndlessLiquids(MapGenerator _generator)
            {
                generator = _generator;

            }
            private bool[,] grid = new bool[countOfQuads, countOfQuads];
            private byte[,] curChunkHeightMap;
            public void UpdateChunk(Vector2Int Vposition)
            {
                for (sbyte xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                {
                    for (sbyte yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                    {
                        curChunkHeightMap = generator.GetMapData(new Vector2Int(Vposition.x + xOffset, Vposition.y + yOffset)).heightMap;
                        for (byte x = 0; x < mapChunkSize; x++)
                            for (byte y = 0; y < mapChunkSize; y++)
                                grid[(xOffset + chunkScale) * mapChunkSize + x, (yOffset + chunkScale) * mapChunkSize + y] = curChunkHeightMap[x, y] <= MapGenerator.waterLevel;
                    }
                }
                GenerateMesh();
                generator.waterT.position = new((Vposition.x - 1) * generationSize, (Vposition.y - 1) * generationSize);
            }

            private void GenerateMesh()
            {
                List<Vector2Int> polygons = ExtractPolygons();
                List<Vector2Int> holes = ExtractPolygons(false);


                BowyerWatsonTriangulation triangulation = new BowyerWatsonTriangulation();
                Mesh mesh = triangulation.GenerateMesh(polygons, holes);

                generator.waterF.mesh = mesh;
            }
            private List<Vector2Int> ExtractPolygons(bool isPolygon = true)
            {
                bool[,] visited = new bool[countOfQuads, countOfQuads];

                List<Vector2Int> polygons = new List<Vector2Int>();

                for (int x = 0; x < countOfQuads; x++)
                {
                    for (int y = 0; y < countOfQuads; y++)
                    {
                        if ((grid[x, y] == isPolygon) && !visited[x, y])
                        {
                            FloodFill(x, y, visited, polygons, isPolygon);
                        }
                    }
                }
                return polygons;
            }

            private void FloodFill(int x, int y, bool[,] visited, List<Vector2Int> polygon, bool isPolygon)
            {
                Stack<Vector2Int> stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(x, y));
                

                while (stack.Count > 0)
                {
                    Vector2Int point = stack.Pop();
                    if (point.x < 0 || point.x >= countOfQuads || point.y < 0 || point.y >= countOfQuads || visited[point.x, point.y] || grid[point.x, point.y] != isPolygon)
                        continue;

                    visited[point.x, point.y] = true;

                    byte rightDistance = 2;

                    if (!isPolygon)
                        if ((point.x + 1 < countOfQuads && grid[point.x + 1, point.y]) ||
                            (point.x - 1 >= 0 && grid[point.x - 1, point.y]) ||
                            (point.y + 1 < countOfQuads && grid[point.x, point.y + 1]) ||
                            (point.y - 1 >= 0 && grid[point.x, point.y - 1]))
                                continue;
                    
                    polygon.Add(point * scale);

                    stack.Push(new Vector2Int(point.x + 1, point.y));
                    stack.Push(new Vector2Int(point.x - 1, point.y));
                    stack.Push(new Vector2Int(point.x, point.y + 1));
                    stack.Push(new Vector2Int(point.x, point.y - 1));
                }
            }
        }

        public class BowyerWatsonTriangulation
        {
            private List<Triangle> triangles = new List<Triangle>();

            public Mesh GenerateMesh(List<Vector2Int> points, List<Vector2Int> holes)
            {
                triangles.Clear();
                triangles = Triangulate(points, holes);

                List<Vector3> vertices = new List<Vector3>();
                List<int> indices = new List<int>();
                List<Vector2> uvs = new List<Vector2>();

                Dictionary<Vector2Int, int> vertexIndexMap = new Dictionary<Vector2Int, int>();

                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;

                foreach (var p in points)
                {
                    if (p.x < minX) minX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y > maxY) maxY = p.y;
                }

                foreach (var tri in triangles)
                {
                    int i1 = GetVertexIndex(tri.A, vertices, vertexIndexMap, uvs, minX, minY, maxX, maxY);
                    int i2 = GetVertexIndex(tri.B, vertices, vertexIndexMap, uvs, minX, minY, maxX, maxY);
                    int i3 = GetVertexIndex(tri.C, vertices, vertexIndexMap, uvs, minX, minY, maxX, maxY);

                    indices.Add(i1);
                    indices.Add(i2);
                    indices.Add(i3);
                }

                Mesh mesh = new Mesh();
                mesh.vertices = vertices.ToArray();
                mesh.triangles = indices.ToArray();
                mesh.uv = uvs.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                return mesh;
            }

            private int GetVertexIndex(Vector2Int point, List<Vector3> vertices, Dictionary<Vector2Int, int> vertexIndexMap,
                                    List<Vector2> uvs, float minX, float minY, float maxX, float maxY)
            {
                if (!vertexIndexMap.TryGetValue(point, out int index))
                {
                    index = vertices.Count;
                    vertices.Add(new Vector3(point.x, point.y, 0));

                    float u = (point.x - minX) / (maxX - minX);
                    float v = (point.y - minY) / (maxY - minY);
                    uvs.Add(new Vector2(u, v));

                    vertexIndexMap[point] = index;
                }
                return index;
            }


            private List<Triangle> Triangulate(List<Vector2Int> points, List<Vector2Int> holes)
            {
                List<Triangle> resultTriangles = new List<Triangle>();
                Triangle superTriangle = CreateSuperTriangle(points);
                resultTriangles.Add(superTriangle);

                foreach (var point in points)
                {
                    List<Triangle> badTriangles = new List<Triangle>();

                    foreach (var triangle in resultTriangles)
                    {
                        if (triangle.CircumcircleContains(point))
                            badTriangles.Add(triangle);
                    }

                    List<Edge> polygon = new List<Edge>();
                    foreach (var tri in badTriangles)
                    {
                        polygon.Add(new Edge(tri.A, tri.B));
                        polygon.Add(new Edge(tri.B, tri.C));
                        polygon.Add(new Edge(tri.C, tri.A));
                    }

                    resultTriangles.RemoveAll(t => badTriangles.Contains(t));
                    polygon = RemoveDuplicateEdges(polygon);

                    foreach (var edge in polygon)
                        resultTriangles.Add(new Triangle(edge.P1, edge.P2, point));
                }

                resultTriangles.RemoveAll(t => t.HasVertex(superTriangle.A) ||
                                            t.HasVertex(superTriangle.B) ||
                                            t.HasVertex(superTriangle.C));
                resultTriangles.RemoveAll(t => IsInsideHole(t, holes));

                return resultTriangles;
            }

            private Triangle CreateSuperTriangle(List<Vector2Int> points)
            {
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;

                foreach (var p in points)
                {
                    if (p.x < minX) minX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y > maxY) maxY = p.y;
                }

                int deltaMax = Mathf.Max(maxX - minX, maxY - minY) * 2;

                Vector2Int A = new Vector2Int(minX - deltaMax, minY - deltaMax);
                Vector2Int B = new Vector2Int(maxX + deltaMax, minY - deltaMax);
                Vector2Int C = new Vector2Int((minX + maxX) / 2, maxY + deltaMax);

                return new Triangle(A, B, C);
            }

            private bool IsInsideHole(Triangle t, List<Vector2Int> holes)
            {
                foreach (var hole in holes)
                {
                    if (t.ContainsPoint(hole))
                        return true;
                }
                return false;
            }

            private List<Edge> RemoveDuplicateEdges(List<Edge> edges)
            {
                List<Edge> uniqueEdges = new List<Edge>();

                foreach (var edge in edges)
                {
                    if (uniqueEdges.Contains(edge))
                        uniqueEdges.Remove(edge);
                    else
                        uniqueEdges.Add(edge);
                }

                return uniqueEdges;
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
                CalculateCircumcircle();
            }

            private void CalculateCircumcircle()
            {
                float ax = A.x, ay = A.y;
                float bx = B.x, by = B.y;
                float cx = C.x, cy = C.y;

                float d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

                float ux = ((ax * ax + ay * ay) * (by - cy) + 
                            (bx * bx + by * by) * (cy - ay) + 
                            (cx * cx + cy * cy) * (ay - by)) / d;

                float uy = ((ax * ax + ay * ay) * (cx - bx) + 
                            (bx * bx + by * by) * (ax - cx) + 
                            (cx * cx + cy * cy) * (bx - ax)) / d;

                center = new Vector2(ux, uy);
                radiusSquared = (center - (Vector2)A).sqrMagnitude;
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
                float w1 = ((B.y - C.y) * (p.x - C.x) + (C.x - B.x) * (p.y - C.y)) /
                        ((B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y));
                float w2 = ((C.y - A.y) * (p.x - C.x) + (A.x - C.x) * (p.y - C.y)) /
                        ((B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y));
                float w3 = 1 - w1 - w2;
                return w1 >= 0 && w2 >= 0 && w3 >= 0;
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