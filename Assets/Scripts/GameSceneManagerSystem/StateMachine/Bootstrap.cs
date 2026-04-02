using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

        [SerializeField] private Button toMenuPauseButton;
        private void Start()
        {
            toMenuPauseButton.onClick.AddListener(SwitchToMainMenu);


            if (isTest)
            {
                gameStateMachine.Initialize(new RavineLogger(null));
                gameStateMachine.StartGame();
                return;
            }
            else
            {
                gameStateMachine.Initialize(ServiceLocator.GetService<RavineLogger>());
            }


            ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ =>
                {
                    ContinueStartFirstPlayer();
                });

            AmbientSystem.Instance.PlayAmbient(AmbientType.Nature_Day).Forget();
        }

        private void ContinueStartFirstPlayer()
        {
            AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());

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
            toMenuPauseButton.onClick.RemoveAllListeners();
        }
    }
}