using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.Base
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UniversalAdditionalCameraData _cameraData;
        private SceneLoader sceneLoader;
        [SerializeField] private GameStateMachine gameStateMachine;
        [SerializeField] private bool isTest;
        private WorldStatePersistence worldStatePersistence;
        private void Start()
        {
            gameStateMachine.Initialize(ServiceLocator.GetService<IRavineLogger>());

            worldStatePersistence = ServiceLocator.GetService<WorldStatePersistence>();
            if (isTest) return;

            ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ =>
                {
                    ContinueStartFirstPlayer();
                });
        }

        private void ContinueStartFirstPlayer()
        {
            AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());

            worldStatePersistence.UpdateState(s => s.gameWon = true);
            if (worldStatePersistence.State.CurrentValue.cycleCount == 0)
            {
                worldStatePersistence.UpdateState(s => s.startTime = DateTimeOffset.Now.ToUnixTimeSeconds());
            }
            sceneLoader = new SceneLoader();

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
            sceneLoader.LoadScene(sceneNumber).Forget();
            AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());
        }

        private void OnDisable()
        {
            InTheEnd(null);
        }
    }
}