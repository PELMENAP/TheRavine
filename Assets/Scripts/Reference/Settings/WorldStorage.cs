using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace TheRavine.Base
{
    public class WorldStorage
    {
        private readonly WorldStateRepository worldStateRepository;
        private readonly WorldConfigRepository worldConfigRepository;

        public WorldStorage(
            WorldStateRepository dataManager,
            WorldConfigRepository settingsManager)
        {
            worldStateRepository = dataManager;
            worldConfigRepository = settingsManager;
        }

        public async UniTask<WorldState> LoadDataAsync(string worldId)
            => await worldStateRepository.LoadAsync(worldId);

        public async UniTask<WorldConfiguration> LoadSettingsAsync(string worldId)
            => await worldConfigRepository.LoadAsync(worldId);

        public async UniTask<(WorldState, WorldConfiguration)> LoadFullAsync(string worldId)
        {
            UniTask<WorldState> dataTask = worldStateRepository.LoadAsync(worldId);
            UniTask<WorldConfiguration> settingsTask = worldConfigRepository.LoadAsync(worldId);

            var (data, settings) = await UniTask.WhenAll(dataTask, settingsTask);
            return (data, settings);
        }

        public async UniTask SaveFullAsync(string worldId, WorldState data, WorldConfiguration settings)
        {
            await worldStateRepository.SaveAsync(worldId, data);
            await worldConfigRepository.SaveAsync(worldId, settings);
        }

        public async UniTask SaveDataAsync(string worldId, WorldState data)
            => await worldStateRepository.SaveAsync(worldId, data);

        public async UniTask SaveSettingsAsync(string worldId, WorldConfiguration settings)
            => await worldConfigRepository.SaveAsync(worldId, settings);

        public async UniTask<bool> ExistsAsync(string worldId)
        {
            return await worldStateRepository.ExistsAsync(worldId);
        }

        public async UniTask DeleteAsync(string worldId)
        {
            await worldStateRepository.DeleteAsync(worldId);
            await worldConfigRepository.DeleteAsync(worldId);
        }

        public async UniTask<IReadOnlyList<string>> GetAllWorldIdsAsync()
        {
            return await worldStateRepository.ListIdsAsync();
        }
    }
}