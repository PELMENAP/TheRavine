using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace TheRavine.Base
{
    public interface IWorldService
    {
        UniTask<WorldData> LoadDataAsync(string worldId);
        UniTask<WorldSettings> LoadSettingsAsync(string worldId);
        UniTask<(WorldData, WorldSettings)> LoadFullAsync(string worldId);
        UniTask SaveFullAsync(string worldId, WorldData data, WorldSettings settings);
        UniTask SaveDataAsync(string worldId, WorldData data);
        UniTask SaveSettingsAsync(string worldId, WorldSettings settings);
        UniTask<bool> ExistsAsync(string worldId);
        UniTask DeleteAsync(string worldId);
        UniTask<List<string>> GetAllWorldIdsAsync();
    }
}