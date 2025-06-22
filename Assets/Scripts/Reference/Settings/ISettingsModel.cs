using R3;
using Cysharp.Threading.Tasks; 

namespace TheRavine.Base
{
    public interface ISettingsModel
    {
        ReadOnlyReactiveProperty<GameSettings> GameSettings { get; }
        ReadOnlyReactiveProperty<WorldSettings> WorldSettings { get; }
        void UpdateGameSettings(GameSettings settings);
        void UpdateWorldSettings(WorldSettings settings);
        UniTask ResetToDefaultsAsync();
        UniTask LoadWorldSettingsAsync(string worldId);
    }
}