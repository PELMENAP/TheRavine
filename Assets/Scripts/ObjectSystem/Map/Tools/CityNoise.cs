using System;

using TheRavine.Extensions;
public static class CityNoise
{
    private static int[,] map;
    private static FastRandom random;
    private static int size;
    private static int[] verticalRoads, horizontalRoads;
    
    public static void InitCityNoise(int _randomSeed, int _size, int numVerticalRoads, int numHorizontalRoads)
    {
        random = new FastRandom(_randomSeed);
        size = _size;
        verticalRoads = new int[numVerticalRoads];
        horizontalRoads = new int[numHorizontalRoads];
    }

    public static void GenerateCityMap(ref int[,] inputMap, int roadWidth, int minRoadSpacing, int numVerticalRoads, int numHorizontalRoads, int riverWidthMin, int riverWidthMax)
    {
        map = inputMap;

        GenerateRiver(riverWidthMin, riverWidthMax);

        GenerateRoads(verticalRoads, numVerticalRoads, roadWidth, minRoadSpacing, true);
        GenerateRoads(horizontalRoads, numHorizontalRoads, roadWidth, minRoadSpacing, false);

        AddBridgeEntrancesAndExits();
        
        CombineBridges();
        
        AddBridgeEntrancesAndExits();
        
        FillRectangleWithFive(minRoadSpacing);
    }

    private static void GenerateRiver(int riverWidthMin, int riverWidthMax)
    {
        int riverWidth = random.Range(riverWidthMin, riverWidthMax + 1);

        int startX = random.Range(size / 4, size / 2);
        int startY = 0;
        int currentX = startX;
        int currentY = startY;

        while (currentY < size)
        {
            for (int w = -riverWidth / 2; w <= riverWidth / 2; w++)
            {
                int x = currentX + w;
                if (x >= 0 && x < size)
                {
                    map[currentY, x] = 2;
                }
            }

            int direction = random.Range(0, 5);
            switch (direction)
            {
                case 0:
                    if (currentX > 0) currentX--;
                    break;
                case 1:
                    if (currentX > 0) currentX--;
                    break;
                case 2:
                    if (currentX < size - 1) currentX++;
                    break;
                case 3:
                    if (currentX < size - 1) currentX++;
                    break;
            }
            currentY++;
        }
    }

    private static void GenerateRoads(int[] roads, int numRoads, int roadWidth, int minRoadSpacing, bool isVertical)
    {
        for (int i = 0; i < numRoads; i++)
        {
            int start;
            bool validPosition;
            do
            {
                start = random.Range(roadWidth, size - roadWidth);
                validPosition = true;
                for (int j = 0; j < i; j++)
                {
                    if (Math.Abs(roads[j] - start) < roadWidth + minRoadSpacing)
                    {
                        validPosition = false;
                        break;
                    }
                }
            } while (!validPosition);
    
            roads[i] = start;
    
            if (isVertical)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int w = 0; w < roadWidth; w++)
                    {
                        int x = start + w;
                        if (x < size)
                        {
                            if (map[y, x] == 2)
                            {
                                map[y, x] = 3;
                            }
                            else
                            {
                                map[y, x] = 1;
                            }
                        }
                    }
                }
            }
            else
            {
                for (int x = 0; x < size; x++)
                {
                    for (int w = 0; w < roadWidth; w++)
                    {
                        int y = start + w;
                        if (y < size)
                        {
                            if (map[y, x] == 2)
                            {
                                map[y, x] = 3;
                            }
                            else
                            {
                                map[y, x] = 1;
                            }
                        }
                    }
                }
            }
        }
    }

    private static void CombineBridges()
    {
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (map[y, x] == 4)
                {
                    if ((map[y - 1, x] == 3 && map[y + 1, x] == 3) ||
                        (map[y - 1, x] == 3 && map[y, x - 1] == 3) ||
                        (map[y - 1, x] == 3 && map[y, x + 1] == 3) ||
                        (map[y + 1, x] == 3 && map[y, x - 1] == 3) ||
                        (map[y + 1, x] == 3 && map[y, x + 1] == 3) ||
                        (map[y + 1, x] == 4))
                    {
                        map[y, x] = 3;
                        if (map[y + 1, x] == 4) map[y + 1, x] = 3;
                    }
                }
            }
        }
    
        for (int y = 0; y < size; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (map[y, x] == 4 && map[y, x - 1] == 3 && map[y, x + 1] == 3)
                {
                    map[y, x] = 3;
                }
                else if (map[y, x] == 4 && map[y, x + 1] == 4)
                {
                    map[y, x] = 3;
                    map[y, x + 1] = 3;
                }
            }
        }
    }

    private static void AddBridgeEntrancesAndExits()
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 1; y < size - 1; y++)
            {
                if (map[y, x] == 3)
                {
                    if (y > 0 && map[y - 1, x] == 1) map[y - 1, x] = 4;
                    if (y < size - 1 && map[y + 1, x] == 1) map[y + 1, x] = 4;
                }
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (map[y, x] == 3)
                {
                    if (x > 0 && map[y, x - 1] == 1) map[y, x - 1] = 4;
                    if (x < size - 1 && map[y, x + 1] == 1) map[y, x + 1] = 4;
                }
            }
        }
    }
    
    private static void FindClosestRoadsToCenter(out int minX, out int maxX, out int minY, out int maxY)
    {
        int center = size / 2;
        
        minX = maxX = verticalRoads[0];
        int minXDist = Math.Abs(center - minX);
        int maxXDist = int.MaxValue;
        
        for (int i = 1; i < verticalRoads.Length; i++)
        {
            int dist = Math.Abs(center - verticalRoads[i]);
            if (dist < minXDist)
            {
                maxX = minX;
                maxXDist = minXDist;
                minX = verticalRoads[i];
                minXDist = dist;
            }
            else if (dist < maxXDist)
            {
                maxX = verticalRoads[i];
                maxXDist = dist;
            }
        }
        
        if (minX > maxX)
        {
            int temp = minX;
            minX = maxX;
            maxX = temp;
        }
        
        minY = maxY = horizontalRoads[0];
        int minYDist = Math.Abs(center - minY);
        int maxYDist = int.MaxValue;
        
        for (int i = 1; i < horizontalRoads.Length; i++)
        {
            int dist = Math.Abs(center - horizontalRoads[i]);
            if (dist < minYDist)
            {
                maxY = minY;
                maxYDist = minYDist;
                minY = horizontalRoads[i];
                minYDist = dist;
            }
            else if (dist < maxYDist)
            {
                maxY = horizontalRoads[i];
                maxYDist = dist;
            }
        }
        
        if (minY > maxY)
        {
            int temp = minY;
            minY = maxY;
            maxY = temp;
        }
    }
    
    private static void FillRectangleWithFive(int minRoadSpacing)
    {
        for(int i = 0; i < minRoadSpacing * minRoadSpacing * minRoadSpacing; i++)
        {
            var pair = GenerateRandomPoint(size, minRoadSpacing);
            int x = pair.Item2, y = pair.Item1;
            if(map[y, x] == 0)
            {
                if(map[y - 1, x] == 0 && map[y + 1, x] == 0)
                {
                    map[y, x] = 1;
                    int n = 0;
                    while(x + n < size - 1 && map[y, x + n + 1] == 0)
                    {
                        n++;
                        map[y, x + n] = 1;
                    }
                    
                    n = 0;
                    while(x + n > 1 && map[y, x + n - 1] == 0)
                    {
                        n--;
                        map[y, x + n] = 1;
                    }
                }
            }
        }
        
        for(int i = 0; i < minRoadSpacing * minRoadSpacing * minRoadSpacing; i++)
        {
            var pair = GenerateRandomPoint(size, minRoadSpacing);
            int x = pair.Item2, y = pair.Item1;
            if(map[y, x] == 0)
            {
                if(map[y, x - 1] == 0 && map[y, x + 1] == 0)
                {
                    map[y, x] = 1;
                    int n = 0;
                    while(y + n < size - 1 && map[y + n + 1, x] == 0)
                    {
                        n++;
                        map[y + n, x] = 1;
                    }
                    
                    n = 0;
                    while(y + n > 1 && map[y + n - 1, x] == 0)
                    {
                        n--;
                        map[y + n, x] = 1;
                    }
                }
            }
        }
        
        FindClosestRoadsToCenter(out int minX, out int maxX, out int minY, out int maxY);

        for (int y = minY + 1; y < maxY; y++)
            for (int x = minX + 1; x < maxX; x++)
                map[y, x] = 5;
    }
    
    public static (int, int) GenerateRandomPoint(int n, int m)
    {
        Random random = new Random();
        
        int maxRadius = (n - m) / 2;

        double angle = random.NextDouble() * 2 * Math.PI;

        int radius = random.Next(m, maxRadius + 1);
        
        int x = n / 2 + (int)Math.Round(radius * Math.Cos(angle));
        int y = n / 2 + (int)Math.Round(radius * Math.Sin(angle));
        
        if(x > n - 2) x = n - 2;
        if(y > n - 2) y = n - 2;
        if(x < 1) x = 1;
        if(y < 1) y = 1;

        return (x, y);
    }
}