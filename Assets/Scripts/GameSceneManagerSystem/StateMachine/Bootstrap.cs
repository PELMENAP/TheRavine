using UnityEngine;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;

using TheRavine.EntityControl;
using TheRavine.Services;

namespace TheRavine.Base
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UniversalAdditionalCameraData _cameraData;
        private SceneTransistor trasitor;
        [SerializeField] private GameStateMachine gameStateMachine;
        private void Awake()
        {
            gameStateMachine.Initialize();

            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
            DataStorage.winTheGame = false;
            if(DataStorage.cycleCount == 0) DataStorage.startTime = Time.time;
            trasitor = new SceneTransistor();

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

        private void InTheEnd(System.Action inTheEndCallback)
        {
            // if(DataStorage.sceneClose) return;
            // DataStorage.sceneClose = true;
            gameStateMachine.BreakUpServices();
            
            DataStorage.sceneClose = false;
            inTheEndCallback?.Invoke();
        }

        private void TransitToOtherScene(int sceneNumber){
            trasitor.LoadScene(sceneNumber).Forget();
            Settings.isLoad = false;
            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        }

        private void OnDisable()
        {
            InTheEnd(null);
        }
    }
}