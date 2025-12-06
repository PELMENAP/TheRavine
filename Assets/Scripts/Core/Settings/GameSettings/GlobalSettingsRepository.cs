using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class GlobalSettingsRepository
    {
        private readonly IAsyncPersistentStorage _storage;
        private const string Key = "global_game_settings";

        public GlobalSettingsRepository(IAsyncPersistentStorage storage)
        {
            _storage = storage;
        }

        public UniTask<bool> ExistsAsync() => _storage.ExistsAsync(Key);

        public UniTask<GlobalSettings> LoadAsync()
            => _storage.LoadAsync<GlobalSettings>(Key);

        public UniTask SaveAsync(GlobalSettings settings)
            => _storage.SaveAsync(Key, settings);
    }
}