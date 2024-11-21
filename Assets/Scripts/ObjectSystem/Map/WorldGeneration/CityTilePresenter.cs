using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

using TheRavine.Base;

public class CityTilePresenter : MonoBehaviour
{
    private System.Threading.CancellationTokenSource _cts = new();
    [SerializeField] private float tileDistance;
    [SerializeField] private int FOW;
    [SerializeField] private Vector2Int offset;
    private GameObject[,] presentMap;
    public async UniTaskVoid PresentMap(GameObject[,] map, List<Transform> _players)
    {
        GenerationUpdate(_players).Forget();
        presentMap = new GameObject[map.GetLength(0), map.GetLength(1)];
        for (byte x = 0; x < map.GetLength(0); x++)
        {
            for (byte y = 0; y < map.GetLength(1); y++)
            {
                Vector3 position = new Vector3(x * tileDistance, y * tileDistance, 0) + this.transform.position;
                GameObject tilePrefab = map[x, y];
                if (tilePrefab != null) 
                {
                    presentMap[x, y] = Instantiate(tilePrefab, position, tilePrefab.transform.rotation, this.transform);
                    presentMap[x, y].SetActive(false);
                }
                await UniTask.Delay(10);
            }
        }
    }

    private async UniTaskVoid GenerationUpdate(List<Transform> players)
    {
        await UniTask.Delay(5000, cancellationToken: _cts.Token);
        List<Vector2Int> oldPlayersPositions = new List<Vector2Int>(players.Count);
        for(byte i = 0; i < players.Count; i++) oldPlayersPositions.Add(Vector2Int.zero);
        Debug.Log(oldPlayersPositions.Count);
        Debug.Log(players.Count);
        while (!DataStorage.sceneClose)
        {
            await UniTask.Delay(1000, cancellationToken: _cts.Token);
            for(int i = 0; i < players.Count; i++)
            {
                Vector2Int playerChunkPosition = new Vector2Int(((int)players[i].position.x + offset.x) / 32, ((int)players[i].position.y + offset.y) / 32);
                
                for(int x = -FOW; x <= FOW; x++)
                    for(int y = -FOW; y <= FOW; y++)
                        if(oldPlayersPositions[i] != null && oldPlayersPositions[i].x + x > 0 && oldPlayersPositions[i].x + x < 50 && oldPlayersPositions[i].y + y > 0 && oldPlayersPositions[i].y + y < 50)
                            presentMap[oldPlayersPositions[i].x + x, oldPlayersPositions[i].y + y]?.SetActive(false);

                for(int x = -FOW; x <= FOW; x++)
                    for(int y = -FOW; y <= FOW; y++)
                        if(playerChunkPosition.x + x > 0 && playerChunkPosition.x + x < 50 && playerChunkPosition.y + y > 0 && playerChunkPosition.y + y < 50)
                            presentMap[playerChunkPosition.x + x, playerChunkPosition.y + y]?.SetActive(true);

                oldPlayersPositions[i] = playerChunkPosition;
            }
        }
    }

    public void ClosePresentation()
    {
        _cts.Cancel();
    }
}
