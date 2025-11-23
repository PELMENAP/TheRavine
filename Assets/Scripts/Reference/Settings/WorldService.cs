using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace TheRavine.Base
{
    public class WorldService
    {
        private readonly WorldFileManager _dataMgr;
        private readonly WorldSettingsManager _settingsMgr;

        public WorldService(
            WorldFileManager dataManager,
            WorldSettingsManager settingsManager)
        {
            _dataMgr = dataManager;
            _settingsMgr = settingsManager;
        }

        public async UniTask<WorldData> LoadDataAsync(string worldId)
            => await _dataMgr.LoadAsync(worldId);

        public async UniTask<WorldSettings> LoadSettingsAsync(string worldId)
            => await _settingsMgr.LoadAsync(worldId);

        public async UniTask<(WorldData, WorldSettings)> LoadFullAsync(string worldId)
        {
            UniTask<WorldData> dataTask = _dataMgr.LoadAsync(worldId);
            UniTask<WorldSettings> settingsTask = _settingsMgr.LoadAsync(worldId);

            var (data, settings) = await UniTask.WhenAll(dataTask, settingsTask);
            return (data, settings);
        }

        public async UniTask SaveFullAsync(string worldId, WorldData data, WorldSettings settings)
        {
            await _dataMgr.SaveAsync(worldId, data);
            await _settingsMgr.SaveAsync(worldId, settings);
        }

        public async UniTask SaveDataAsync(string worldId, WorldData data)
            => await _dataMgr.SaveAsync(worldId, data);

        public async UniTask SaveSettingsAsync(string worldId, WorldSettings settings)
            => await _settingsMgr.SaveAsync(worldId, settings);

        public async UniTask<bool> ExistsAsync(string worldId)
        {
            return await _dataMgr.ExistsAsync(worldId);
        }

        public async UniTask DeleteAsync(string worldId)
        {
            await _dataMgr.DeleteAsync(worldId);
            await _settingsMgr.DeleteAsync(worldId);
        }

        public async UniTask<IReadOnlyList<string>> GetAllWorldIdsAsync()
        {
            return await _dataMgr.ListIdsAsync();
        }
    }
}