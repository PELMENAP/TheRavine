using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;
public class EndlessTerrainOld : MonoBehaviour
{
    #region [CONST & STATIC]
    const int itemSize = 16;
    const int scale = 5;
    const float sqrViewerMoveThresholdForChunkUpdate = 40f;
    const int chunksVisibleInViewDst = 1;
    public static EndlessTerrainOld instance;
    static int chunkSize = 16;
    static int objuseLength;
    #endregion

    public List<Vector2> loosers = new List<Vector2>();

    [SerializeField] private GameObject mapFragment, terra, water;
    [SerializeField] private bool drawMap, placeObj;
    [SerializeField] private MapGeneratorOld mapGenerator;
    [SerializeField] private Vector2 viewerPositionOld, viewerPosition;
    [SerializeField] private Transform viewer;
    [SerializeField] private Sprite watSprite, terSprite;
    [SerializeField] private Objnorm[] objnorm;
    [SerializeField] private GameObject[] objuse;
    [SerializeField] private GameObject[] structs;
    [SerializeField] private int[] objusenum;
    [SerializeField] private int[] maxStructObject;
    private int[] objusenumUpdate;
    private List<Vector2> terrainChunksVisibleUpdate = new List<Vector2>(9);
    private List<Vector2> terrainChunksVisibleLastUpdate = new List<Vector2>(9);
    private Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>(100);

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
            PoolManager.inst.CreatePool(objuse[i], objusenum[i]);
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
                        PoolManager.inst.CreatePool(objuse[j], objusenumUpdate[j] - objusenum[j]);
                        objusenum[j] = objusenumUpdate[j];
                    }
                }
                foreach (var item in terrainChunkDictionary[terrainChunksVisibleUpdate[i]].land)
                {
                    if (loosers.Contains(item.Key))
                    {
                        PoolManager.inst.ReuseObjectToPosition(objuse[item.Value].GetInstanceID(), item.Key);
                    }
                    else
                    {
                        PoolManager.inst.ReuseObjectToPosition(objuse[item.Value].GetInstanceID(), item.Key);
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
        public int[] numLand = new int[EndlessTerrainOld.objuseLength];
        private Vector2 pos;
        private GameObject mapFragment, terra, water;

        public TerrainChunk(Vector2 position, Transform parent)
        {
            pos = new Vector2((int)(-position.x), (int)position.y);
            mapFragment = Instantiate(EndlessTerrainOld.instance.mapFragment, new Vector3(pos.x - 10, pos.y - 10, 20) * scale + new Vector3(0, 0, -1), Quaternion.Euler(0, 0, 180), parent);
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
                    listObj = EndlessTerrainOld.instance.objnorm[objectMap[x, y]];
                    terraRect = listObj.ground.textureRect;
                    for (int h = 0; h < itemSize; h++)
                    {
                        for (int w = 0; w < itemSize; w++)
                        {
                            EndlessTerrainOld.instance.colorTerrainTexture[y * itemSize + h, x * itemSize + w] = EndlessTerrainOld.instance.colorSpriteTerra[(h + (int)terraRect.y) * 4 * itemSize + w + (int)terraRect.x];
                        }
                    }
                    if (objectMap[x, y] <= 1 && isWater)
                    {
                        for (int h = 0; h < itemSize; h++)
                        {
                            for (int w = 0; w < itemSize; w++)
                            {
                                EndlessTerrainOld.instance.colorWaterTexture[y * itemSize + h, x * itemSize + w] = EndlessTerrainOld.instance.colorSpriteWater[h * itemSize + w];
                            }
                        }
                    }
                    if (listObj.structsList != null)
                    {
                        for (int i = 0; i < listObj.structsList.Length; i++)
                        {
                            indexObj = Array.IndexOf(EndlessTerrainOld.instance.structs, listObj.structsList[i]);
                            if ((x * noiseMap[x, y] + y * noiseMap[x, y] - i) % listObj.chance[listObj.objList.Length + i] == 1 && EndlessTerrainOld.instance.maxStructObject[indexObj] != 0)
                            {
                                if (EndlessTerrainOld.instance.maxStructObject[indexObj] > 0)
                                    EndlessTerrainOld.instance.maxStructObject[indexObj]--;
                                Instantiate(EndlessTerrainOld.instance.structs[indexObj], new Vector2(place.x + -x * scale, place.y + -y * scale), Quaternion.identity);
                                isStruct = true;
                                break;
                            }
                        }
                    }
                    if (listObj.objList != null && !isStruct)
                    {
                        for (int i = 0; i < listObj.objList.Length; i++)
                        {
                            indexObj = Array.IndexOf(EndlessTerrainOld.instance.objuse, listObj.objList[i]);
                            if ((x * noiseMap[x, y] + y * noiseMap[x, y] - i) % listObj.chance[i] == 1)
                            {
                                posobj = new Vector2(place.x + -x * scale, place.y + -y * scale);
                                if (listObj.spread[i])
                                    posobj += new Vector2((x * MapGeneratorOld.seed + place.x) / (i + 1) % 4, (y * MapGeneratorOld.seed + place.y) / (i + 1) % 4 - 10);
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
            terra = Instantiate(EndlessTerrainOld.instance.terra, new Vector3(pos.x - 10, pos.y - 10, 0) * scale, Quaternion.Euler(0, 0, 180), parent);
            EndlessTerrainOld.instance.terraSprite.SetPixels(EndlessTerrainOld.instance.colorTerrainTexture.Cast<Color>().ToArray());
            EndlessTerrainOld.instance.terraSprite.Apply();
            terra.GetComponent<SpriteRenderer>().sprite = Sprite.Create(EndlessTerrainOld.instance.terraSprite, new Rect(0.0f, 0.0f, EndlessTerrainOld.instance.terraSprite.width, EndlessTerrainOld.instance.terraSprite.height), new Vector2(0.5f, 0.5f), 8f);
            if (isWater)
            {
                water = Instantiate(EndlessTerrainOld.instance.water, new Vector3(pos.x - 10f, pos.y - 10f, 0) * scale, Quaternion.Euler(0, 0, 180), parent);
                EndlessTerrainOld.instance.waterSprite.SetPixels(EndlessTerrainOld.instance.colorWaterTexture.Cast<Color>().ToArray());
                EndlessTerrainOld.instance.waterSprite.Apply();
                water.GetComponent<SpriteRenderer>().sprite = Sprite.Create(EndlessTerrainOld.instance.waterSprite, new Rect(0.0f, 0.0f, EndlessTerrainOld.instance.waterSprite.width, EndlessTerrainOld.instance.waterSprite.height), new Vector2(0.5f, 0.5f), 8f);
            }
            EndlessTerrainOld.instance.terraSprite = new Texture2D(chunkSize * itemSize, chunkSize * itemSize);
            EndlessTerrainOld.instance.waterSprite = new Texture2D(chunkSize * itemSize, chunkSize * itemSize);
        }

        private void OnMapDataReceived(Transform parent)
        {
            if (-pos.x >= 200 || -pos.x <= -200 || pos.y >= 200 || pos.y <= -200)
                return;
            MapData mapData = EndlessTerrainOld.instance.mapGenerator.GetMapData(new Vector2(-pos.x, pos.y));
            if (EndlessTerrainOld.instance.drawMap)
                mapFragment.GetComponent<SpriteRenderer>().sprite = Sprite.Create(TextureGenerator.TextureFromColourMap(mapData.colourMap, chunkSize, chunkSize), new Rect(0, 0, chunkSize, chunkSize), new Vector2(0.5f, 0.5f));
            if (EndlessTerrainOld.instance.placeObj)
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