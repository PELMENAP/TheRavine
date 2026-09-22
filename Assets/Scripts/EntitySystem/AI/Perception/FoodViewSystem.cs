using Cysharp.Threading.Tasks;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public sealed class FoodViewSystem : MonoBehaviour
{
    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private int poolSize = 128;
    [SerializeField] private int pollIntervalMs = 250;

    private MapGenerator   _map;
    private ObjectSystem   _objects;
    private ChunkFoodIndex _index;

    private long _lastChunk;
    private int  _seenRevision = -1;
    private long _seenChunk = long.MinValue;
    private int  _seenVersionSum;

    private async void Start()
    {
        _map     = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
        _objects = await ServiceLocator.WaitUntilServiceReady<ObjectSystem>();
        _index   = await ServiceLocator.WaitUntilServiceReady<ChunkFoodIndex>();

        _objects.CreatePool(ChunkFoodIndex.FoodPrefabId, foodPrefab, poolSize);
        _map.onUpdate += OnChunkUpdate;

        PollLoopAsync().Forget();
    }

    private void OnDestroy()
    {
        if (_map != null) _map.onUpdate -= OnChunkUpdate;
    }

    private void OnChunkUpdate(long playerChunk)
    {
        _lastChunk = playerChunk;
        TryRefresh();
    }

    private async UniTaskVoid PollLoopAsync()
    {
        while (!destroyCancellationToken.IsCancellationRequested)
        {
            await UniTask.Delay(pollIntervalMs, cancellationToken: destroyCancellationToken);
            TryRefresh();
        }
    }

    private void TryRefresh()
    {
        if (_map == null || _objects == null) return;

        int revision = _index != null ? _index.Revision : 0;
        int versionSum = 0;
        const int r = MapGenerator.chunkScale;

        for (int cx = -r; cx <= r; cx++)
        for (int cz = -r; cz <= r; cz++)
            if (_map.TryGetChunk(Position2Int.Offset(_lastChunk, cx, cz), out ChunkData cd) && cd != null)
                versionSum += cd.Version;

        if (revision == _seenRevision && _lastChunk == _seenChunk && versionSum == _seenVersionSum)
            return;

        _seenRevision   = revision;
        _seenChunk      = _lastChunk;
        _seenVersionSum = versionSum;

        Refresh();
    }

    private void Refresh()
    {
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