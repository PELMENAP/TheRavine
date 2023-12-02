using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using System.Collections;

public class SceneTransition : MonoBehaviour
{
    private bool _isLoading = false;
    [SerializeField] private Camera _camera;

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
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        while (waitFading)
        {
            yield return null;
        }

        SceneManager.LoadScene(numberSceneToTranslate);

        _isLoading = false;

        Destroy(this.gameObject);
    }

    public void AddCameraToStack(Camera _cameraToAdd)
    {
        var cameraData = _camera.GetUniversalAdditionalCameraData();
        cameraData.cameraStack.Add(_cameraToAdd);
    }
}
