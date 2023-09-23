using UnityEngine;
public class MenuMainScript : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject settings;
    [SerializeField] private SceneTransition trasitor;
    private void Awake()
    {
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


    public void MoveMenu()
    {
        menu.SetActive(settings.activeInHierarchy);
        settings.SetActive(!menu.activeInHierarchy);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}