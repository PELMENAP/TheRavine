using UnityEngine;
using TheRavine.Generator;

public static class Voronoi
{
    private static Vector2[] points;
    private static int pointsCount;

    public static void SetInit(int _pointsCount)
    {
        pointsCount = _pointsCount;
        points = new Vector2[pointsCount];
    }

    public static void GenerateVoronoiDiagram(ref float[,] diagram, int seed, int mapChunkSize, Vector2 offset)
    {
        System.Random prng = new System.Random(seed);

        // Генерируем случайные точки
        for (int i = 0; i < pointsCount; i++)
        {
            float x = prng.Next(0, mapChunkSize) + offset.x;
            float y = prng.Next(0, mapChunkSize) + offset.y;
            points[i] = new Vector2(x, y);
        }

        float maxDistance = Mathf.Sqrt(mapChunkSize * mapChunkSize * 2); // Максимально возможное расстояние на карте

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                Vector2 pixelPosition = new Vector2(x, y) + offset;
                float minDistance = maxDistance;

                // Ищем ближайшую точку
                for (int i = 0; i < pointsCount; i++)
                {
                    float distance = Vector2.Distance(pixelPosition, points[i]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }

                // Нормализуем значение, чтобы оно было в пределах от 0 до 1
                diagram[x, y] = minDistance / maxDistance;
            }
        }
    }
}