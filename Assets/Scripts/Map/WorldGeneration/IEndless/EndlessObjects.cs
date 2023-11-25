using System.Collections.Generic;
using UnityEngine;

public class EndlessObjects : IEndless
{
    private MapGenerator generator;
    private const int chunkCount = MapGenerator.chunkCount;
    private Vector2[] terrainChunksVisibleUpdate = new Vector2[chunkCount * chunkCount];
    private List<Vector2> terrainChunksVisibleLastUpdate = new List<Vector2>();
    public EndlessObjects(MapGenerator _generator)
    {
        generator = _generator;
    }
    public void UpdateChunk(Vector3 Vposition)
    {
        int currentChunkCoordX = Mathf.RoundToInt(Vposition.x);
        int currentChunkCoordY = Mathf.RoundToInt(Vposition.y);
        int countOfIteration = 0;
        for (int yOffset = 0; yOffset < chunkCount; yOffset++)
        {
            for (int xOffset = 0; xOffset < chunkCount; xOffset++)
            {
                Vector2 chunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                terrainChunksVisibleUpdate[countOfIteration] = chunkCoord;
                if (terrainChunksVisibleLastUpdate.Contains(chunkCoord))
                    continue;
                List<Vector2> map = generator.GetMapData(new Vector2(-chunkCoord.x, chunkCoord.y)).objectsToInst;
                foreach (var item in map)
                {
                    ObjectInstInfo info = ObjectSystem.inst.GetGlobalObjectInfo(item);
                    if (info.name == "therivinetop")
                        continue;
                    ObjectSystem.inst.Reuse(info.prefabID, item);
                }
                countOfIteration++;
            }
        }
        terrainChunksVisibleLastUpdate.Clear();
        terrainChunksVisibleLastUpdate.AddRange(terrainChunksVisibleUpdate);
        // Debug.Log(countOfIteration);
    }

    private Texture2D texture;
    private Texture2D TextureFromColourMap(Color[] colourMap, int width, int height)
    {
        texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        return texture;
    }
}