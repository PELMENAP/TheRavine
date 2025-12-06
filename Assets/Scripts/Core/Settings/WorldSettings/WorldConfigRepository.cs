using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class WorldConfigRepository
    {
        private readonly IAsyncPersistentStorage _storage;

        public WorldConfigRepository(IAsyncPersistentStorage storage)
        {
            _storage = storage;
        }

        private string GetKey(string worldId) => $"world_settings_{worldId}";

        public UniTask<bool> ExistsAsync(string worldId)
            => _storage.ExistsAsync(GetKey(worldId));

        public UniTask<WorldConfiguration> LoadAsync(string worldId)
            => _storage.LoadAsync<WorldConfiguration>(GetKey(worldId));

        public UniTask SaveAsync(string worldId, WorldConfiguration settings)
            => _storage.SaveAsync(GetKey(worldId), settings);

        public UniTask DeleteAsync(string worldId)
            => _storage.DeleteAsync(GetKey(worldId));
    }
}