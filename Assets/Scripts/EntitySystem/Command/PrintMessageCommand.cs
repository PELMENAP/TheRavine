using Cysharp.Threading.Tasks;
using UnityEngine;

public class PrintMessageCommand : IScheduledCommand
{
    private string _message;
    private System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();

    public PrintMessageCommand(string message)
    {
        _message = message;
    }

    public async UniTask ExecuteAsync()
    {
        Debug.Log($"Start executing: {_message}");

        await UniTask.Delay(2000, cancellationToken: _cts.Token);
        if (_cts.IsCancellationRequested) return;
        Debug.Log(_message);
    }

    public void Cancel()
    {
        _cts.Cancel();
    }
}
