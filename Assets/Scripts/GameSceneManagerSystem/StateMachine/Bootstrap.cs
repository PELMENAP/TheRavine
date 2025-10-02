using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;

using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UniversalAdditionalCameraData _cameraData;
        private SceneLoader trasitor;
        [SerializeField] private GameStateMachine gameStateMachine;
        [SerializeField] private bool isTest;
        private WorldDataService worldDataService;
        private async void Start()
        {
            worldDataService =ServiceLocator.GetService<WorldDataService>();
            if (isTest) return;
            gameStateMachine.Initialize();

            while (!gameStateMachine.HaveServiceLocatorPlayer())
            {
                gameStateMachine.LogBootstrapInfo("There is no players in the scene");
                await UniTask.Delay(1000);
            }

            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());

            worldDataService.SetGameWon(false);
            if (worldDataService.WorldData.CurrentValue.cycleCount == 0)
            {
                worldDataService.SetTime(DateTimeOffset.Now.ToUnixTimeSeconds());
            }
            trasitor = new SceneLoader();

            gameStateMachine.StartGame();
        }

        public void AddCameraToStack(Camera _cameraToAdd)
        {
            try
            {
                _cameraData.cameraStack.Add(_cameraToAdd);
            }
            catch
            {
                Debug.LogWarning("There is no camera data to add camera");
            }
        }

        public void SwitchToMainMenu(){
            InTheEnd(() => TransitToOtherScene(0));
        }
        public void SwitchToParallaxScene(){
            InTheEnd(() => TransitToOtherScene(1));
        }

        private void InTheEnd(Action inTheEndCallback)
        {
            // if(DataStorage.sceneClose) return;
            // DataStorage.sceneClose = true;
            gameStateMachine.BreakUpServices();
            inTheEndCallback?.Invoke();
        }

        private void TransitToOtherScene(int sceneNumber){
            trasitor.LoadScene(sceneNumber).Forget();
            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        }

        private void OnDisable()
        {
            InTheEnd(null);
        }
    }
}