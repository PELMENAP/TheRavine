using UnityEngine.SceneManagement;
using UnityEngine;
using System.Collections;

public class SceneTransition : MonoBehaviour
{
    private bool _isLoading = false;

    public IEnumerator LoadScene(int numberSceneToTranslate, bool isLoad)
    {
        if (_isLoading)
        {
            yield return null;
        }
        DontDestroyOnLoad(this);
        Settings.SceneNumber = numberSceneToTranslate;
        Settings.isLoad = isLoad;
        _isLoading = true;
        bool waitFading = true;
        FaderOnTransit.instance.FadeIn(() => waitFading = false);
        while (waitFading)
        {
            yield return null;
        }

        SceneManager.LoadScene(numberSceneToTranslate);

        _isLoading = false;

        Destroy(this.gameObject);
    }

}
