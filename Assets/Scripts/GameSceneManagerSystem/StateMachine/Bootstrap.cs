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
            SaveAndLeaveAsync().Forget();
        }

        private async UniTask SaveAndLeaveAsync()
        {
            var entities = FindAnyObjectByType<EntityManager>();
            bool resume = entities != null && entities.enabled;
            if (resume) entities.enabled = false;
            if (WorldAuthority.IsAuthority && ServiceLocator.Services.TryGet(out WorldRegistry registry) &&
                registry.HasLoadedWorld && !await registry.SaveCurrentWorldAsync())
            {
                if (resume) entities.enabled = true;
                _transitStarted = false;
                toMenuPauseButton.interactable = true;
                return;
            }
            AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());
            sceneLoader.LoadScene(0, () => ReleaseServices(restoreGlobalServices: true)).Forget();
        }

        private void ReleaseServices(bool restoreGlobalServices)
        {
            if (_servicesReleased) return;
            _servicesReleased = true;

            EntityManager entities = FindAnyObjectByType<EntityManager>();
            if (entities != null)
            {
                entities.enabled = false;
                RunStep(entities.DropCarriedObjects, "drop carried objects");
            }

            RunStep(BreakUpSceneServices, "break up");
            if (entities != null) RunStep(entities.ReleaseServices, "entity services");
            RunStep(ShutdownNetwork, "network shutdown");
            RunStep(ServiceLocator.ClearAll, "clear services");
            if (restoreGlobalServices) RunStep(RestoreGlobalServices, "global services");
        }

        private void BreakUpSceneServices()
        {
            if (gameStateMachine != null) gameStateMachine.BreakUpServices();
        }

        private static void ShutdownNetwork()
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }

        private static void RestoreGlobalServices()
        {
            if (GameInitializer.Instance != null) GameInitializer.Instance.InitializeServices();
        }

        private static void RunStep(Action step, string name)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Bootstrap.ReleaseServices: {name} failed: {ex}");
            }
        }

        private void OnDisable()
        {
            ReleaseServices(restoreGlobalServices: false);
            if (toMenuPauseButton != null) toMenuPauseButton.onClick.RemoveAllListeners();
        }
    }
}