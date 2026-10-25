using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using TheRavine.Generator;

namespace TheRavine.Base
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UniversalAdditionalCameraData _cameraData;
        private SceneLoader sceneLoader;
        [SerializeField] private GameStateMachine gameStateMachine;
        [SerializeField] private bool isTest;

        [SerializeField] private Button toMenuPauseButton;
        private bool _transitStarted;
        private bool _servicesReleased;
        private void Start()
        {
            sceneLoader = new SceneLoader();
            toMenuPauseButton.onClick.AddListener(SwitchToMainMenu);


            if (isTest)
            {
                if (!ServiceLocator.Services.TryGet(out RavineLogger logger))
                {
                    logger = new RavineLogger(null);
                    ServiceLocator.Register(logger);
                }
                gameStateMachine.Initialize(logger);
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

            gameStateMachine.StartGame();
        }

        public void AddCameraToStack(Camera _cameraToAdd)
        {
            try
            {
                if (!_cameraData.cameraStack.Contains(_cameraToAdd)) _cameraData.cameraStack.Add(_cameraToAdd);
            }
            catch
            {
                RavineLog.Warning("There is no camera data to add camera");
            }
        }

        public void SwitchToMainMenu()
        {
            if (_transitStarted || sceneLoader == null) return;

            _transitStarted = true;
            toMenuPauseButton.interactable = false;
            AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());
            sceneLoader.LoadScene(0, ReleaseServices).Forget();
        }

        private void ReleaseServices()
        {
            if (_servicesReleased) return;
            _servicesReleased = true;

            gameStateMachine.BreakUpServices();
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            ServiceLocator.ClearAll();
            if (GameInitializer.Instance != null) GameInitializer.Instance.InitializeServices();
        }

        private void OnDisable()
        {
            ReleaseServices();
            toMenuPauseButton.onClick.RemoveAllListeners();
        }
    }
}