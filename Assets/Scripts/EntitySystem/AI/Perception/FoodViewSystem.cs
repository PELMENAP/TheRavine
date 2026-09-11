using Cysharp.Threading.Tasks;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public sealed class FoodViewSystem : MonoBehaviour
{
    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private int poolSize = 128;
    [SerializeField] private int refreshIntervalMs = 500;

    private MapGenerator _map;
    private ObjectSystem _objects;
    private long _lastChunk;

    private async void Start()
    {
        _map     = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
        _objects = await ServiceLocator.WaitUntilServiceReady<ObjectSystem>();

        _objects.CreatePool(ChunkFoodIndex.FoodPrefabId, foodPrefab, poolSize);
        _map.onUpdate += OnChunkUpdate;

        RefreshLoopAsync().Forget();
    }

    private void OnDestroy()
    {
        if (_map != null) _map.onUpdate -= OnChunkUpdate;
    }

    private void OnChunkUpdate(long playerChunk)
    {
        _lastChunk = playerChunk;
        Refresh();
    }

    private async UniTaskVoid RefreshLoopAsync()
    {
        while (!destroyCancellationToken.IsCancellationRequested)
        {
            await UniTask.Delay(refreshIntervalMs, cancellationToken: destroyCancellationToken);
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_map == null || _objects == null) return;

        int used = 0;
        const int r = MapGenerator.chunkScale;

        for (int cx = -r; cx <= r; cx++)
        for (int cz = -r; cz <= r; cz++)
        {
            if (!_map.TryGetChunk(Position2Int.Offset(_lastChunk, cx, cz), out ChunkData cd) || cd == null)
                continue;

            for (int i = 0; i < cd.Objects.Length; i++)
            {
                ObjectInstInfo info = cd.Objects[i];
                if (info.PrefabID != ChunkFoodIndex.FoodPrefabId) continue;

                _objects.Reuse(ChunkFoodIndex.FoodPrefabId, info.Position);
                used++;
            }
        }

        int excess = _objects.GetPoolSize(ChunkFoodIndex.FoodPrefabId) - used;
        for (int i = 0; i < excess; i++)
            _objects.Deactivate(ChunkFoodIndex.FoodPrefabId);
    }
}