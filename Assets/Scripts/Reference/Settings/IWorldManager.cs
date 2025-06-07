using System;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;


namespace TheRavine.Base
{
    public interface IWorldManager
    {
        string CurrentWorldName { get; }
        ObservableList<string> AvailableWorlds { get; }
        Observable<string> CurrentWorld { get; }
        UniTask<bool> CreateWorldAsync(string worldName, WorldSettings customSettings = null);
        UniTask<bool> LoadWorldAsync(string worldName);
        UniTask<bool> DeleteWorldAsync(string worldName);
        UniTask RefreshWorldListAsync();
    }

}