using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace TheRavine.Base
{
    public interface IWorldDataService
    {
        Observable<WorldData> WorldDataObserver { get; }
        ReadOnlyReactiveProperty<WorldData> WorldData { get; }
        UniTask<bool> SaveWorldDataAsync();
        UniTask<bool> LoadWorldDataAsync(string worldName);
        UniTask ForceUpdateSeed();
        void UpdateWorldData(WorldData data);
        void UpdatePlayerPosition(Vector3 position);
        void IncrementCycle();
        void SetTime(float time);
        void SetGameWon(bool won);
    }
}