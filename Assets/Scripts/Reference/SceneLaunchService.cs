using UnityEngine;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;
using System;
using R3;
public sealed class SceneLaunchService
{
    private readonly SceneLoader loader;
    private readonly Camera faderCamera;
    private readonly ReactiveProperty<bool> isBusy;

    private readonly int gameSceneIndex;
    private readonly int testSceneIndex;
    private readonly UniversalAdditionalCameraData cameraData;

    public bool CanLaunch => !isBusy.Value;

    public SceneLaunchService(
        int gameIndex,
        int testIndex,
        SceneLoader sceneLoader,
        UniversalAdditionalCameraData cameraData,
        Camera faderCamera,
        ReactiveProperty<bool> isBusyFlag)
    {
        this.loader = sceneLoader;
        this.cameraData = cameraData;
        this.gameSceneIndex = gameIndex;
        this.testSceneIndex = testIndex;
        this.faderCamera = faderCamera;
        this.isBusy = isBusyFlag;
    }

    public UniTask LaunchGame() =>
        LaunchScene(gameSceneIndex);

    public UniTask LaunchTest() =>
        LaunchScene(testSceneIndex);

    public async UniTask LaunchScene(int index)
    {
        if (isBusy.Value) return;

        isBusy.Value = true;
        AddCameraToStack(faderCamera);

        try
        {
            await loader.LoadScene(index);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Ошибка загрузки сцены {index}: {ex.Message}");
            isBusy.Value = false;
        }
    }

    private void AddCameraToStack(Camera cam)
    {
        if (cam != null && cameraData != null)
            cameraData.cameraStack.Add(cam);
    }
}
