using Cysharp.Threading.Tasks;
using System.Collections.Generic;


namespace TheRavine.Base
{
    public class WorldFileManager : IFileManager<string, WorldData>
    {
        private readonly IAsyncPersistentStorage _storage;
        private const string WorldsListKey = "worlds_list";
        private const string WorldKeyPrefix  = "world_data_";

        public WorldFileManager(IAsyncPersistentStorage storage)
        {
            _storage = storage;
        }

        public async UniTask SaveAsync(string worldId, WorldData data)
        {
            string key = WorldKeyPrefix + worldId;
            await _storage.SaveAsync(key, data);

            var list = await ListIdsAsync() as List<string>;
            if (!list.Contains(worldId))
            {
                list.Add(worldId);
                await _storage.SaveAsync(WorldsListKey, list);
            }
        }

        public async UniTask<WorldData> LoadAsync(string worldId)
        {
            string key = WorldKeyPrefix + worldId;
            return await _storage.LoadAsync<WorldData>(key);
        }

        public async UniTask<bool> ExistsAsync(string worldId)
        {
            string key = WorldKeyPrefix + worldId;
            return await _storage.ExistsAsync(key);
        }

        public async UniTask DeleteAsync(string worldId)
        {
            string key = WorldKeyPrefix + worldId;
            await _storage.DeleteAsync(key);

            var list = await ListIdsAsync() as List<string>;
            if (list.Remove(worldId))
                await _storage.SaveAsync(WorldsListKey, list);
        }

        public async UniTask<IReadOnlyList<string>> ListIdsAsync()
        {
            var list = await _storage.LoadAsync<List<string>>(WorldsListKey);
            return list ?? new List<string>();
        }
    }
}