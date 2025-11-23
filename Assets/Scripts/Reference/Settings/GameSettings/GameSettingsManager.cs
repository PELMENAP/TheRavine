using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class GameSettingsManager
    {
        private readonly IAsyncPersistentStorage _storage;
        private const string Key = "global_game_settings";

        public GameSettingsManager(IAsyncPersistentStorage storage)
        {
            _storage = storage;
        }

        public UniTask<bool> ExistsAsync() => _storage.ExistsAsync(Key);

        public UniTask<GameSettings> LoadAsync()
            => _storage.LoadAsync<GameSettings>(Key);

        public UniTask SaveAsync(GameSettings settings)
            => _storage.SaveAsync(Key, settings);
    }
}