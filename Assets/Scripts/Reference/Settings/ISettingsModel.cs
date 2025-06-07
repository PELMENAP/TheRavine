using R3;

namespace TheRavine.Base
{
    public interface ISettingsModel
    {
        ReadOnlyReactiveProperty<GameSettings> GameSettings { get; }
        ReadOnlyReactiveProperty<WorldSettings> WorldSettings { get; }
        void UpdateGameSettings(GameSettings settings);
        void UpdateWorldSettings(WorldSettings settings);
        void ResetToDefaults();
    }
}