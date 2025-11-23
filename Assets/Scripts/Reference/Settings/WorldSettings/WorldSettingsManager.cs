using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class WorldSettingsManager
    {
        private readonly IAsyncPersistentStorage _storage;

        public WorldSettingsManager(IAsyncPersistentStorage storage)
        {
            _storage = storage;
        }

        private string GetKey(string worldId) => $"world_settings_{worldId}";

        public UniTask<bool> ExistsAsync(string worldId)
            => _storage.ExistsAsync(GetKey(worldId));

        public UniTask<WorldSettings> LoadAsync(string worldId)
            => _storage.LoadAsync<WorldSettings>(GetKey(worldId));

        public UniTask SaveAsync(string worldId, WorldSettings settings)
            => _storage.SaveAsync(GetKey(worldId), settings);

        public UniTask DeleteAsync(string worldId)
            => _storage.DeleteAsync(GetKey(worldId));
    }
}