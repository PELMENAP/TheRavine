using UnityEngine;
using UnityEngine.Rendering.Universal;

using TheRavine.Base;
public class MenuMainScript : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject settings;
    [SerializeField] private UniversalAdditionalCameraData _cameraData;
    private SceneTransitor trasitor;

    private void Awake()
    {
        trasitor = new SceneTransitor();
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        FaderOnTransit.instance.FadeOut(() => Init());
    }

    private void Init()
    {
        menu.SetActive(true);
        settings.SetActive(false);
        settings.GetComponent<Settings>().SetInitialValues();
    }


    public void StartGame()
    {
        trasitor.LoadScene(2).Forget();
        Settings.isLoad = false;
    }
    public void LoadGame()
    {
        trasitor.LoadScene(2).Forget();
        Settings.isLoad = true;
    }

    public void LoadTestScene()
    {
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
        menu.SetActive(true);
        settings.SetActive(false);
    }

    public void MoveToSettings()
    {
        menu.SetActive(false);
        settings.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}