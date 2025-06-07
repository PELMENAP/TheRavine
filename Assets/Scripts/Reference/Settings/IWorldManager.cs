using System;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;


namespace TheRavine.Base
{
    public interface IWorldManager
    {
        string CurrentWorldName { get; }
        Observable<string> CurrentWorld { get; }
        ISynchronizedViewList<string> AvailableWorlds { get; }
        
        UniTask<bool> CreateWorldAsync(string worldName);
        UniTask<bool> LoadWorldAsync(string worldName);
        UniTask<bool> DeleteWorldAsync(string worldName);
        UniTask RefreshWorldListAsync();
    }

}