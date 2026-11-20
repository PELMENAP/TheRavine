using Cysharp.Threading.Tasks;
using System.Threading;

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

        public UniTask<bool> ExistsAsync(CancellationToken ct = default) => _storage.ExistsAsync(Key, ct);

        public UniTask<GlobalSettings> LoadAsync(CancellationToken ct = default)
            => _storage.LoadAsync<GlobalSettings>(Key, ct);

        public UniTask SaveAsync(GlobalSettings settings)
            => _storage.SaveAsync(Key, settings);
    }
}