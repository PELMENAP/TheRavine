using UnityEngine;
using Unity.Netcode;
using System;

using Cysharp.Threading.Tasks;
public interface INetworkConnectionManager
{
    UniTask<bool> StartHostAsync();
    UniTask<bool> StartServerAsync();
    UniTask<bool> StartClientAsync();
    void Disconnect();
}
public class NetworkConnectionManager : INetworkConnectionManager
{
    private readonly SceneTransistor sceneLoader;
    private readonly int targetSceneIndex;

    public NetworkConnectionManager(SceneTransistor sceneLoader, int targetSceneIndex)
    {
        this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
        this.targetSceneIndex = targetSceneIndex;
    }

    public async UniTask<bool> StartHostAsync()
    {
        return await StartNetworkModeAsync(() => NetworkManager.Singleton.StartHost());
    }

    public async UniTask<bool> StartServerAsync()
    {
        return await StartNetworkModeAsync(() => NetworkManager.Singleton.StartServer());
    }

    public async UniTask<bool> StartClientAsync()
    {
        return await StartNetworkModeAsync(() => NetworkManager.Singleton.StartClient());
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    private async UniTask<bool> StartNetworkModeAsync(Func<bool> startAction)
    {
        try
        {
            await sceneLoader.LoadScene(targetSceneIndex);

            await UniTask.Delay(1000);
            return startAction.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start network mode: {ex.Message}");
            return false;
        }
    }
}