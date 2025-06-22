using System;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;
using System.Collections.Generic;


namespace TheRavine.Base
{
    public interface IWorldManager : IDisposable
    {
        ObservableList<string> AvailableWorlds { get; }
        string CurrentWorldName { get; }
        Observable<string> CurrentWorld { get; }
        Observable<bool> IsLoading { get; }
        Observable<long> CacheVersion { get; }
        
        UniTask<bool> CreateWorldAsync(string worldName, WorldSettings customSettings = null);
        UniTask<bool> LoadWorldAsync(string worldName);
        UniTask<bool> DeleteWorldAsync(string worldName);
        UniTask<bool> RenameWorldAsync(string oldName, string newName);
        UniTask<bool> DuplicateWorldAsync(string sourceName, string newName);
        UniTask RefreshWorldListAsync();
        UniTask<WorldInfo> GetWorldInfoAsync(string worldName, bool useCache = true);
        UniTask<List<WorldInfo>> GetAllWorldsInfoAsync(bool useCache = true);
    }

}