using Cysharp.Threading.Tasks;

public interface IScheduledCommand
{
    UniTask ExecuteAsync();
    void Cancel();
    bool CanExecute() => true; 
}
