using UnityEngine;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
public class MenuMainScript : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject settings;
    [SerializeField] private UniversalAdditionalCameraData _cameraData;
    private SceneTransistor trasitor;
    private bool isInit;
    private void Awake()
    {
        trasitor = new SceneTransistor();
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        isInit = true;
        FaderOnTransit.instance.FadeOut(() => Init());
    }

    private void Init()
    {
        menu.SetActive(true);
        settings.SetActive(false);
        settings.GetComponent<Settings>().SetInitialValues();
        isInit = false;
    }


    public void StartGame()
    {
        if(isInit) return;
        trasitor.LoadScene(2).Forget();
        Settings.isLoad = false;
        DataStorage.cycleCount = 0;
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
    }
    public void LoadGame()
    {
        if(isInit) return;
        trasitor.LoadScene(2).Forget();
        Settings.isLoad = true;
        // DataStorage.cycleCount = 1;
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
    }

    public void LoadTestScene()
    {
        if(isInit) return;
        trasitor.LoadScene(2).Forget();
        Settings.isLoad = false;
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
    }

    public void AddCameraToStack(Camera _cameraToAdd)
    {
        _cameraData.cameraStack.Add(_cameraToAdd);
    }


    public void MoveToMenu()
    {
        if(isInit) return;
        menu.SetActive(true);
        settings.SetActive(false);
    }

    public void MoveToSettings()
    {
        if(isInit) return;
        menu.SetActive(false);
        settings.SetActive(true);
    }

    public void QuitGame()
    {
        if(isInit) return;
        Application.Quit();
    }
}