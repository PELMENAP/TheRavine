using Cysharp.Threading.Tasks;
using UnityEngine;

public interface ICommand
{
    UniTask ExecuteAsync();
    void Cancel();
}
