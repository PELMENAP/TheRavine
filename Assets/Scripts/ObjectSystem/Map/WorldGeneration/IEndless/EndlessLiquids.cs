using UnityEngine;

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
                ushort dotCount = 0, trianglCount = 0;
                int[] triangles = new int[countOfQuads * countOfQuads * 6];
                for (byte x = 0; x < countOfQuads; x++)
                {
                    for (byte y = 0; y < countOfQuads; y++)
                    {
                        Vector3 basePos = new(x * scale, y * scale);
                        vertices[dotCount] = basePos;
                        vertices[dotCount + 1] = basePos + Vector3.up * scale;
                        vertices[dotCount + 2] = basePos + Vector3.up * scale + Vector3.right * scale;
                        vertices[dotCount + 3] = basePos + Vector3.right * scale;

                        triangles[trianglCount] = dotCount;
                        triangles[trianglCount + 1] = dotCount + 1;
                        triangles[trianglCount + 2] = dotCount + 2;
                        triangles[trianglCount + 3] = dotCount;
                        triangles[trianglCount + 4] = dotCount + 2;
                        triangles[trianglCount + 5] = dotCount + 3;
                        dotCount += 4;
                        trianglCount += 6;
                    }
                }
                generator.waterF.mesh.SetVertices(vertices);
                generator.waterF.mesh.SetTriangles(triangles, 0);
                generator.waterF.mesh.Optimize();
            }
            private bool[,] meshMap = new bool[countOfQuads, countOfQuads];
            public void UpdateChunk(Vector2 Vposition)
            {
                for (sbyte xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                {
                    for (sbyte yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                    {
                        byte[,] map = generator.GetMapData(new Vector2(Vposition.x + xOffset, Vposition.y + yOffset)).heightMap;
                        for (byte x = 0; x < mapChunkSize; x++)
                            for (byte y = 0; y < mapChunkSize; y++)
                                meshMap[(xOffset + chunkScale) * mapChunkSize + x, (yOffset + chunkScale) * mapChunkSize + y] = map[x, y] <= MapGenerator.waterLevel;
                    }
                }
                GetQuadWaterMeshMap();
                generator.waterT.position = new((Vposition.x - 1) * generationSize, (Vposition.y - 1) * generationSize);
            }
            private readonly Vector2[] uv = new Vector2[4 * countOfQuads * countOfQuads];
            private readonly Vector3[] vertices = new Vector3[4 * countOfQuads * countOfQuads];
            private const float diff = 0.1f, mdiff = 1f - diff;
            private Vector2 difZero = new(diff, 0), zeroDif = new(0, diff), anarchist = new(diff, diff), komunist = new(diff, mdiff), skinhed = new(mdiff, mdiff), kapitalist = new(mdiff, diff);
            private void GetQuadWaterMeshMap()
            {
                ushort dotCount = 0;
                for (byte x = 0; x < countOfQuads; x++)
                {
                    for (byte y = 0; y < countOfQuads; y++)
                    {
                        Vector3 basePos = new(x * scale, y * scale);
                        vertices[dotCount] = basePos;
                        vertices[dotCount + 1] = basePos + Vector3.up * scale;
                        vertices[dotCount + 2] = basePos + Vector3.up * scale + Vector3.right * scale;
                        vertices[dotCount + 3] = basePos + Vector3.right * scale;

                        if (meshMap[x, y])
                        {
                            uv[dotCount] = anarchist;
                            uv[dotCount + 1] = komunist;
                            uv[dotCount + 2] = skinhed;
                            uv[dotCount + 3] = kapitalist;
                            bool[] sides = {
                                x + 1 >= countOfQuads || !meshMap[x + 1, y],
                                x - 1 < 0 || !meshMap[x - 1, y],
                                y + 1 >= countOfQuads || !meshMap[x, y + 1],
                                y - 1 < 0 || !meshMap[x, y - 1],
                                x + 1 >= countOfQuads || y + 1 >= countOfQuads || !meshMap[x + 1, y + 1],
                            };
                            
                            if (sides[0])
                            {
                                uv[dotCount + 2] += difZero;
                                uv[dotCount + 3] += difZero;
                            }

                            if (sides[1])
                            {
                                uv[dotCount] -= difZero;
                                uv[dotCount + 1] -= difZero;
                            }

                            if (sides[2])
                            {
                                uv[dotCount + 1] += zeroDif;
                                uv[dotCount + 2] += zeroDif;
                            }

                            if (sides[3])
                            {
                                uv[dotCount] -= zeroDif;
                                uv[dotCount + 3] -= zeroDif;
                            }

                            if(sides[0] && sides[2] && sides[4])
                            {
                                vertices[dotCount + 2] -= Vector3.up * scale / 4 + Vector3.right * scale / 4;
                            }
                        }
                        else
                        {
                            uv[dotCount] = Vector2.zero;
                            uv[dotCount + 1] = Vector2.zero;
                            uv[dotCount + 2] = Vector2.zero;
                            uv[dotCount + 3] = Vector2.zero;
                        }
                        dotCount += 4;
                    }
                }
                generator.waterF.mesh.SetUVs(0, uv);
                generator.waterF.mesh.SetVertices(vertices);
            }
        }
    }
}