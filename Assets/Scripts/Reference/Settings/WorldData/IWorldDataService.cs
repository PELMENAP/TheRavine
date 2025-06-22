using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace TheRavine.Base
{
    public interface IWorldDataService
    {
        Observable<WorldData> WorldData { get; }
        UniTask<bool> SaveWorldDataAsync();
        UniTask<bool> LoadWorldDataAsync(string worldName);
        void UpdateWorldData(WorldData data);
        void UpdatePlayerPosition(Vector3 position);
        void IncrementCycle();
        void SetGameWon(bool won);
    }
}