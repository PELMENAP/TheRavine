using UnityEngine;

using TheRavine.Base;
public class MenuMainScript : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject settings;
    [SerializeField] private SceneTransition trasitor;
    private void Awake()
    {
        menu.SetActive(true);
        settings.SetActive(false);
        settings.GetComponent<Settings>().SetInitialValues();
    }

    public void StartGame()
    {
        StartCoroutine(trasitor.LoadScene(2, false));
    }
    public void LoadGame()
    {
        StartCoroutine(trasitor.LoadScene(2, true));
    }

    public void LoadTestScene()
    {
        StartCoroutine(trasitor.LoadScene(1, false));
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