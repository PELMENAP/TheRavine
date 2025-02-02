using System;
using System.Collections.Generic;

using TheRavine.Extensions;

public static class CityStructures
{
    private static int[,] map;
    private static FastRandom random;
    private static int size;
    private static List<int[,]> structures;

    public static void InitCityStructures(int[,] cityMap, int _randomSeed)
    {
        map = cityMap;
        size = cityMap.GetLength(0);
        random = new FastRandom(_randomSeed);
        structures = new List<int[,]>();

        // Define various structures (5 marks the structure, -1 marks reserved spaces)
        structures.Add(new int[,] { { 5, -1 }, { -1, -1 } }); // 2x2 square
        structures.Add(new int[,] { { 5, 5, -1 }, { -1, -1, -1 } }); // 3x2 rectangle
        structures.Add(new int[,] { { 5, 5, 5 }, { -1, -1, -1 }, { -1, -1, -1 } }); // 3x3 square
        structures.Add(new int[,] { { 5, 5, 5, -1 }, { -1, -1, -1, -1 } }); // 4x3 rectangle
    }

    public static void PlaceStructures(int numStructures)
    {
        for (int i = 0; i < numStructures; i++)
        {
            var structure = SelectRandomStructure();
            if (!TryPlaceStructure(structure))
            {
                Console.WriteLine("No suitable position found for the structure.");
            }
        }
    }

    private static int[,] SelectRandomStructure()
    {
        return structures[random.Range(0, structures.Count)];
    }

    private static bool TryPlaceStructure(int[,] structure)
    {
        int structHeight = structure.GetLength(0);
        int structWidth = structure.GetLength(1);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            int startX = random.Range(0, size - structWidth);
            int startY = random.Range(0, size - structHeight);

            if (CanPlaceStructure(startX, startY, structure))
            {
                PlaceStructure(startX, startY, structure);
                return true;
            }
        }

        return false;
    }

    private static bool CanPlaceStructure(int startX, int startY, int[,] structure)
    {
        int structHeight = structure.GetLength(0);
        int structWidth = structure.GetLength(1);

        for (int x = 0; x < structWidth; x++)
        {
            for (int y = 0; y < structHeight; y++)
            {
                if (map[startX + x, startY + y] != 0)
                {
                    return false; // Space is not empty
                }
            }
        }

        return true;
    }

    private static void PlaceStructure(int startX, int startY, int[,] structure)
    {
        int structHeight = structure.GetLength(0);
        int structWidth = structure.GetLength(1);

        for (int x = 0; x < structWidth; x++)
        {
            for (int y = 0; y < structHeight; y++)
            {
                map[startX + x, startY + y] = structure[y, x];
            }
        }
    }

    public static int[,] GetCityMap() => map;
}