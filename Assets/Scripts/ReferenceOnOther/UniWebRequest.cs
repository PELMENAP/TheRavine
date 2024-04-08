using UnityEngine;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;

public class WebRequest : MonoBehaviour
{
    [SerializeField] private string url;
    [Button]
    private async void SendRequest()
    {
        var result = await GetTextAsync(url, this.GetCancellationTokenOnDestroy());
        print(result);
    }

    private async UniTask<string> GetTextAsync(string url, CancellationToken token)
    {
        var op = await UnityWebRequest.Get(url).SendWebRequest().WithCancellation(token);
        return op.downloadHandler.text;
    }
}
