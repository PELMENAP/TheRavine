using UnityEngine;
using System.Collections.Generic;
using System;

using TheRavine.Services;
using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public class CityMapGenerator : MonoBehaviour, ISetAble
    {
        [SerializeField] private CityTilePresenter tilePresenter;
        [SerializeField] private HeightType[] heightTypes;
        [SerializeField] private StructType[] structTypes;
        [SerializeField] private int size, randomSeed;
        private List<List<Func<int[,], int, int, bool>>> compareList;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            FillCompareList();
            int[,] map = GenerateMap(); 
            GameObject[,] objectMap = CompareMap(ref map);
            AddStructs(ref objectMap, ref map);
            tilePresenter.PresentMap(objectMap, locator.GetPlayersTransforms()).Forget();
        }

        private int[,] GenerateMap()
        {
            FastRandom rand = new FastRandom(UnityEngine.Random.Range(0, 1000));

            int roadWidth = 1;
            int minRoadSpacing = 3;
            int numVerticalRoads = rand.Range(3, 8);
            int numHorizontalRoads = rand.Range(4, 10);
            int riverWidthMin = 3;
            int riverWidthMax = 4;
            // randomSeed = rand.Range(0, 100000);

            int[,] cityMap = new int[size, size];
            CityNoise.InitCityNoise(randomSeed, size, numVerticalRoads, numHorizontalRoads);
            CityNoise.GenerateCityMap(ref cityMap, roadWidth, minRoadSpacing, numVerticalRoads, numHorizontalRoads, riverWidthMin, riverWidthMax);
            return cityMap;
        }

        private GameObject[,] CompareMap(ref int[,] map)
        {
            int[,] structMap = new int[size, size];
            GameObject[,] objectMap = new GameObject[size, size];

            for (int x = 1; x < size - 1; x++)
            {
                for (int y = 1; y < size - 1; y++)
                {
                    int height = map[x, y];
                    List<Func<int[,], int, int, bool>> curCompList = compareList[height];
                    for(int i = 0; i < curCompList.Count; i++)
                        if(curCompList[i](map, x, y))
                        {
                            if(height == 0 && i == 0)
                            {
                                structMap[x, y] = -1;
                            }
                            else if(height == 5 && i == 0)
                            {
                                structMap[x, y] = -2;
                            }
                            objectMap[x, y] = heightTypes[height].tileTypes[i].prefab;
                            break;
                        }
                }   
            }
            map = structMap;

            return objectMap; 
        }

        private void AddStructs(ref GameObject[,] objectMap, ref int[,] map)
        {
            for (int x = 1; x < size - 1; x++)
            {
                for (int y = 1; y < size - 1; y++)
                {
                    if(x + 1 >= size || y - 2 < 0)
                    {
                        continue;
                    }
                    if(x + 1 >= size || y - 2 < 0 || map[x + 1, y - 2] != 1)
                    {
                        print(map[x + 1, y - 2]);
                        continue;
                    }
                    if(map[x, y] == -1)
                    {
                        for(int i = 0; i < structTypes.Length; i++)
                        {
                            bool isPlaseable = true;
                            for(int x_s = 0; x_s < structTypes[i].x; x_s++)
                            {
                                for(int y_s = 0; y_s < structTypes[i].y; y_s++)
                                {
                                    if(x + x_s < size && y + y_s < size)
                                    {
                                        if(map[x + x_s, y + y_s] != -1)
                                        {
                                            isPlaseable = false;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        isPlaseable = false;
                                        break;
                                    }
                                }   
                            }     

                            if(isPlaseable)
                            {
                                for(int x_s = 0; x_s < structTypes[i].x; x_s++)
                                    for(int y_s = 0; y_s < structTypes[i].y; y_s++)
                                    {
                                        map[x + x_s, y + y_s] = 0;
                                        objectMap[x + x_s, y + y_s] = null;
                                    }

                                Vector2Int[] vector2Int = structTypes[i].vector2s;
                                
                                for(int k = 0; k < vector2Int.Length; k++)
                                {
                                    map[x + vector2Int[k].x, y + vector2Int[k].y] = 0;
                                    objectMap[x + vector2Int[k].x, y + vector2Int[k].y] = null;
                                }
                                
                                objectMap[x, y] = structTypes[i].prefab;
                            }
                        }
                    }
                }
            }
        }

        private void FillCompareList()
        {
            compareList = new List<List<Func<int[,], int, int, bool>>>(6);
            compareList.Add(new List<Func<int[,], int, int, bool>>());
            compareList[0].Add(CityTileRules.GrassRule0); 
            compareList[0].Add(CityTileRules.GrassRule1); 
            compareList[0].Add(CityTileRules.GrassRule2); 
            compareList[0].Add(CityTileRules.GrassRule3); 
            compareList[0].Add(CityTileRules.GrassRule4); 
            compareList[0].Add(CityTileRules.GrassRule5); 
            compareList[0].Add(CityTileRules.GrassRule6); 
            compareList[0].Add(CityTileRules.GrassRule7); 
            compareList[0].Add(CityTileRules.GrassRule8); 
            compareList[0].Add(CityTileRules.GrassRule9); 
            compareList[0].Add(CityTileRules.GrassRule10); 
            compareList[0].Add(CityTileRules.GrassRule11); 
            compareList[0].Add(CityTileRules.GrassRule12); 
            compareList[0].Add(CityTileRules.GrassRule13); 
            compareList[0].Add(CityTileRules.GrassRule14); 
            compareList[0].Add(CityTileRules.GrassRule15); 
            compareList.Add(new List<Func<int[,], int, int, bool>>());
            compareList[1].Add(CityTileRules.RoadRule0); 
            compareList[1].Add(CityTileRules.RoadRule1); 
            compareList[1].Add(CityTileRules.RoadRule2); 
            compareList[1].Add(CityTileRules.RoadRule3); 
            compareList[1].Add(CityTileRules.RoadRule4); 
            compareList[1].Add(CityTileRules.RoadRule5); 
            compareList[1].Add(CityTileRules.RoadRule6);
            compareList[1].Add(CityTileRules.RoadRule7); 
            compareList[1].Add(CityTileRules.RoadRule8); 
            compareList[1].Add(CityTileRules.RoadRule9); 
            compareList[1].Add(CityTileRules.RoadRule10);
            compareList.Add(new List<Func<int[,], int, int, bool>>());
            compareList[2].Add(CityTileRules.RiverRule0); 
            compareList[2].Add(CityTileRules.RiverRule1); 
            compareList[2].Add(CityTileRules.RiverRule2); 
            compareList[2].Add(CityTileRules.RiverRule3); 
            compareList[2].Add(CityTileRules.RiverRule4); 
            compareList[2].Add(CityTileRules.RiverRule5); 
            compareList[2].Add(CityTileRules.RiverRule6); 
            compareList[2].Add(CityTileRules.RiverRule7); 
            compareList[2].Add(CityTileRules.RiverRule8); 
            compareList[2].Add(CityTileRules.RiverRule9); 
            compareList[2].Add(CityTileRules.RiverRule10); 
            compareList[2].Add(CityTileRules.RiverRule11); 
            compareList[2].Add(CityTileRules.RiverRule12); 
            compareList.Add(new List<Func<int[,], int, int, bool>>());
            compareList[3].Add(CityTileRules.BridgeRule0); 
            compareList[3].Add(CityTileRules.BridgeRule1); 
            compareList[3].Add(CityTileRules.BridgeRule2); 
            compareList[3].Add(CityTileRules.BridgeRule3); 
            compareList[3].Add(CityTileRules.BridgeRule4); 
            compareList[3].Add(CityTileRules.BridgeRule5);
            compareList[3].Add(CityTileRules.BridgeRule6); 
            compareList.Add(new List<Func<int[,], int, int, bool>>());
            compareList[4].Add(CityTileRules.AwayBridgeRule0); 
            compareList[4].Add(CityTileRules.AwayBridgeRule1); 
            compareList[4].Add(CityTileRules.AwayBridgeRule2); 
            compareList[4].Add(CityTileRules.AwayBridgeRule3); 
            compareList.Add(new List<Func<int[,], int, int, bool>>());
            compareList[5].Add(CityTileRules.SquareRule0); 
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            tilePresenter.ClosePresentation();
            compareList.Clear();
            callback?.Invoke();
        }
    }

    [System.Serializable]
    public struct HeightType
    {
        public TileType[] tileTypes;
    }

    [System.Serializable]
    public struct TileType
    {
        public GameObject prefab;
    }

    [System.Serializable]
    public struct StructType
    {
        public int x, y;
        public GameObject prefab;
        public Vector2Int[] vector2s;
    }
}