using Cysharp.Threading.Tasks;

public interface ICommand
{
    UniTask ExecuteAsync();
    void Cancel();
    bool CanExecute() => true; 
}
