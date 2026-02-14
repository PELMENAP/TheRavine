using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

using R3;

namespace TheRavine.Base
{
    public class AutosaveSystem : IDisposable
    {
        private readonly List<Func<UniTask<bool>>> saveActions;
        private readonly IRavineLogger logger;
        private int intervalSeconds;
        private bool stillRepeated = false;

        public AutosaveSystem(IRavineLogger logger, int initialInterval = 10)
        {
            saveActions = new List<Func<UniTask<bool>>>();
            this.logger = logger;
            intervalSeconds = initialInterval;
        }
        
        public void AddSaveAction(Func<UniTask<bool>> saveAction)
        {
            if (saveAction == null)
            {
                logger.LogWarning("Попытка добавить null действие сохранения");
                return;
            }

            logger.LogWarning("Автодействие сохранено");
            
            saveActions.Add(saveAction);
        }
        
        public bool RemoveSaveAction(Func<UniTask<bool>> saveAction)
        {
            return saveActions.Remove(saveAction);
        }
        
        public void ClearSaveActions()
        {
            saveActions.Clear();
        }
        
        public void SetInterval(int seconds)
        {
            if (seconds < 0)
            {
                logger.LogWarning("Интервал автосохранения не может быть отрицательным");
                return;
            }
            intervalSeconds = seconds;
        }

        public void Pause() => stillRepeated = false;
        public void Start()
        {
            stillRepeated = true;
            RestartTimer().Forget();
        }
        
        private async UniTaskVoid RestartTimer()
        {
            while(stillRepeated)
            {
                logger.LogWarning("цикл автосохранения");
                for (int i = 0; i < saveActions.Count; i++)
                {
                    saveActions[i]().Forget();
                }
                
                await UniTask.Delay(intervalSeconds * 1000);
            }
        }
        
        public void Dispose()
        {
        }
    }
}