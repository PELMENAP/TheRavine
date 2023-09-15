using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;
public class EndlessTerrain : MonoBehaviour
{
    #region [CONST & STATIC]
    const int itemSize = 16;
    const int scale = 5;
    const float sqrViewerMoveThresholdForChunkUpdate = 40f;
    const int chunksVisibleInViewDst = 1;
    public static EndlessTerrain instance;
    static int chunkSize = 16;
    static int objuseLength;
    #endregion

    public List<Vector2> loosers = new List<Vector2>();

    [SerializeField] private GameObject mapFragment, terra, water;
    [SerializeField] private bool drawMap, placeObj;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private Vector2 viewerPositionOld, viewerPosition;
    [SerializeField] private Transform viewer;
    [SerializeField] private Sprite watSprite, terSprite;
    [SerializeField] private Objnorm[] objnorm;
    [SerializeField] private GameObject[] objuse;
    [SerializeField] private GameObject[] structs;
    [SerializeField] private int[] objusenum;
    [SerializeField] private int[] maxStructObject;
    private int[] objusenumUpdate;
    private List<Vector2> terrainChunksVisibleUpdate = new List<Vector2>();
    private List<Vector2> terrainChunksVisibleLastUpdate = new List<Vector2>();
    private Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();

    private Color[] colorSpriteWater, colorSpriteTerra;
    private Color[,] colorTerrainTexture, colorWaterTexture;
    private Texture2D terraSprite, waterSprite;
    #region [MONO]
    private void Awake()
    {
        colorTerrainTexture = new Color[chunkSize * itemSize, chunkSize * itemSize];
        terraSprite = new Texture2D(chunkSize * itemSize, chunkSize * itemSize);
        colorWaterTexture = new Color[chunkSize * itemSize, chunkSize * itemSize];
        waterSprite = new Texture2D(chunkSize * itemSize, chunkSize * itemSize);
        colorSpriteWater = watSprite.texture.GetPixels();
        colorSpriteTerra = terSprite.texture.GetPixels();
        instance = this;
    }

    private void Start()
    {
        objuseLength = objuse.Length;
        objusenumUpdate = new int[objuseLength];
        for (int i = 0; i < objuseLength; i++)
        {
            PoolManager.instance.CreatePool(objuse[i], objusenum[i], false);
        }
        viewerPosition = new Vector2(-viewer.position.x - 50f, viewer.position.y + 50f) / scale;
        StartCoroutine(UpdateVisibleChunks());
    }

    private void Update()
    {
        viewerPosition = new Vector2(-viewer.position.x - 50f, viewer.position.y + 50f) / scale;
        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
            StartCoroutine(UpdateVisibleChunks());
    }
    #endregion

    private IEnumerator UpdateVisibleChunks()
    {
        viewerPositionOld = viewerPosition;
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);
        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                terrainChunksVisibleUpdate.Add(new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset));
            }
        }
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            if (!terrainChunksVisibleUpdate.Contains(terrainChunksVisibleLastUpdate[i]))
                terrainChunkDictionary[terrainChunksVisibleLastUpdate[i]].SetTerrain(false);
        }
        for (int i = 0; i < terrainChunksVisibleUpdate.Count; i++)
        {
            if (!terrainChunkDictionary.ContainsKey(terrainChunksVisibleUpdate[i]))
            {
                terrainChunkDictionary.Add(terrainChunksVisibleUpdate[i], new TerrainChunk(terrainChunksVisibleUpdate[i] * chunkSize, transform));
                yield return new WaitForSeconds(0.2f);
            }
            try
            {
                for (int j = 0; j < terrainChunkDictionary[terrainChunksVisibleUpdate[i]].numLand.Length; j++)
                {
                    objusenumUpdate[j] += terrainChunkDictionary[terrainChunksVisibleUpdate[i]].numLand[j];
                    if (objusenumUpdate[j] > objusenum[j])
                    {
                        PoolManager.instance.CreatePool(objuse[j], objusenumUpdate[j] - objusenum[j], objusenum[j] != 0);
                        objusenum[j] = objusenumUpdate[j];
                    }
                }
                foreach (var item in terrainChunkDictionary[terrainChunksVisibleUpdate[i]].land)
                {
                    if (loosers.Contains(item.Key))
                    {
                        PoolManager.instance.ReuseObject(objuse[item.Value], item.Key, false);
                    }
                    else
                    {
                        PoolManager.instance.ReuseObject(objuse[item.Value], item.Key, (item.Key - new Vector2(viewer.position.x, viewer.position.y)).sqrMagnitude < 8000f);
                    }
                }
                terrainChunkDictionary[terrainChunksVisibleUpdate[i]].SetTerrain(true);
            }
            catch
            {
            }
        }
        terrainChunksVisibleLastUpdate.Clear();
        terrainChunksVisibleLastUpdate.AddRange(terrainChunksVisibleUpdate.ToArray());
        terrainChunksVisibleUpdate.Clear();
        Array.Clear(objusenumUpdate, 0, objusenumUpdate.Length);
        yield return new WaitForSeconds(0.00001f);
    }

    #region [Chunk]
    public class TerrainChunk
    {
        public Dictionary<Vector2, int> land = new Dictionary<Vector2, int>();
        public int[] numLand = new int[EndlessTerrain.objuseLength];
        private Vector2 pos;
        private GameObject mapFragment, terra, water;

        public TerrainChunk(Vector2 position, Transform parent)
        {
            pos = new Vector2((int)(-position.x), (int)position.y);
            mapFragment = Instantiate(EndlessTerrain.instance.mapFragment, new Vector3(pos.x - 10, pos.y - 10, 20) * scale + new Vector3(0, 0, -1), Quaternion.Euler(0, 0, 180), parent);
            OnMapDataReceived(parent);
        }

        private void PlaceObject(int[,] objectMap, float[,] noiseMap, Vector2 place, Transform parent)
        {
            bool isWater = false, isStruct = false;
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    if (objectMap[x, y] <= 1)
                    {
                        isWater = true;
                        break;
                    }
                }
            }
            int indexObj;
            Objnorm listObj;
            Vector2 posobj;
            Rect terraRect;
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    listObj = EndlessTerrain.instance.objnorm[objectMap[x, y]];
                    terraRect = listObj.ground.textureRect;
                    for (int h = 0; h < itemSize; h++)
                    {
                        for (int w = 0; w < itemSize; w++)
                        {
                            EndlessTerrain.instance.colorTerrainTexture[y * itemSize + h, x * itemSize + w] = EndlessTerrain.instance.colorSpriteTerra[(h + (int)terraRect.y) * 4 * itemSize + w + (int)terraRect.x];
                        }
                    }
                    if (objectMap[x, y] <= 1 && isWater)
                    {
                        for (int h = 0; h < itemSize; h++)
                        {
                            for (int w = 0; w < itemSize; w++)
                            {
                                EndlessTerrain.instance.colorWaterTexture[y * itemSize + h, x * itemSize + w] = EndlessTerrain.instance.colorSpriteWater[h * itemSize + w];
                            }
                        }
                    }
                    if (listObj.structsList != null)
                    {
                        for (int i = 0; i < listObj.structsList.Length; i++)
                        {
                            indexObj = Array.IndexOf(EndlessTerrain.instance.structs, listObj.structsList[i]);
                            if ((x * noiseMap[x, y] + y * noiseMap[x, y] - i) % listObj.chance[listObj.objList.Length + i] == 1 && EndlessTerrain.instance.maxStructObject[indexObj] != 0)
                            {
                                if (EndlessTerrain.instance.maxStructObject[indexObj] > 0)
                                    EndlessTerrain.instance.maxStructObject[indexObj]--;
                                Instantiate(EndlessTerrain.instance.structs[indexObj], new Vector2(place.x + -x * scale, place.y + -y * scale), Quaternion.identity);
                                isStruct = true;
                                break;
                            }
                        }
                    }
                    if (listObj.objList != null && !isStruct)
                    {
                        for (int i = 0; i < listObj.objList.Length; i++)
                        {
                            indexObj = Array.IndexOf(EndlessTerrain.instance.objuse, listObj.objList[i]);
                            if ((x * noiseMap[x, y] + y * noiseMap[x, y] - i) % listObj.chance[i] == 1)
                            {
                                posobj = new Vector2(place.x + -x * scale, place.y + -y * scale);
                                if (listObj.spread[i])
                                    posobj += new Vector2((x * MapGenerator.seed + place.x) / (i + 1) % 4, (y * MapGenerator.seed + place.y) / (i + 1) % 4 - 10);
                                for (int xoffset = -3; xoffset <= 3; xoffset++)
                                {
                                    for (int yoffset = -3; yoffset <= 3; yoffset++)
                                    {
                                        try
                                        {
                                            land.Add(posobj + new Vector2(xoffset / 2, yoffset / 2), indexObj);
                                            xoffset = 4;
                                            break;
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                }
                                numLand[indexObj] += 1;
                            }
                        }
                    }
                }
            }
            terra = Instantiate(EndlessTerrain.instance.terra, new Vector3(pos.x - 10, pos.y - 10, 0) * scale, Quaternion.Euler(0, 0, 180), parent);
            EndlessTerrain.instance.terraSprite.SetPixels(EndlessTerrain.instance.colorTerrainTexture.Cast<Color>().ToArray());
            EndlessTerrain.instance.terraSprite.Apply();
            terra.GetComponent<SpriteRenderer>().sprite = Sprite.Create(EndlessTerrain.instance.terraSprite, new Rect(0.0f, 0.0f, EndlessTerrain.instance.terraSprite.width, EndlessTerrain.instance.terraSprite.height), new Vector2(0.5f, 0.5f), 8f);
            if (isWater)
            {
                water = Instantiate(EndlessTerrain.instance.water, new Vector3(pos.x - 10f, pos.y - 10f, 0) * scale, Quaternion.Euler(0, 0, 180), parent);
                EndlessTerrain.instance.waterSprite.SetPixels(EndlessTerrain.instance.colorWaterTexture.Cast<Color>().ToArray());
                EndlessTerrain.instance.waterSprite.Apply();
                water.GetComponent<SpriteRenderer>().sprite = Sprite.Create(EndlessTerrain.instance.waterSprite, new Rect(0.0f, 0.0f, EndlessTerrain.instance.waterSprite.width, EndlessTerrain.instance.waterSprite.height), new Vector2(0.5f, 0.5f), 8f);
            }
            EndlessTerrain.instance.terraSprite = new Texture2D(chunkSize * itemSize, chunkSize * itemSize);
            EndlessTerrain.instance.waterSprite = new Texture2D(chunkSize * itemSize, chunkSize * itemSize);
        }

        private void OnMapDataReceived(Transform parent)
        {
            if (-pos.x >= 200 || -pos.x <= -200 || pos.y >= 200 || pos.y <= -200)
                return;
            MapData mapData = EndlessTerrain.instance.mapGenerator.GetMapData(new Vector2(-pos.x, pos.y));
            if (EndlessTerrain.instance.drawMap)
                mapFragment.GetComponent<SpriteRenderer>().sprite = Sprite.Create(TextureGenerator.TextureFromColourMap(mapData.colourMap, chunkSize, chunkSize), new Rect(0, 0, chunkSize, chunkSize), new Vector2(0.5f, 0.5f));
            if (EndlessTerrain.instance.placeObj)
                PlaceObject(mapData.objectMap, mapData.heightMap, new Vector2(pos.x, pos.y) * scale, parent);
        }

        public void SetTerrain(bool active)
        {
            if (terra != null)
            {
                terra.SetActive(active);
            }
            if (water != null)
            {
                water.SetActive(active);
            }
        }
    }
    #endregion

    [System.Serializable]
    public struct Objnorm
    {
        public Sprite ground;
        public GameObject[] objList;
        public GameObject[] structsList;
        public int[] chance;
        public bool[] spread;
    }
}