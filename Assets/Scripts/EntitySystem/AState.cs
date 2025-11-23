using System;
using Cysharp.Threading.Tasks;

public delegate void Behaviour();

public abstract class AState : IDisposable
{
    protected CommandScheduler _commandProcessor = new CommandScheduler();

    public void AddCommand(ICommand command)
    {
        _commandProcessor.AddCommand(command);
    }

    public async UniTask ProcessCommandsAsync()
    {
        await _commandProcessor.ProcessCommandsAsync();
    }

    public void CancelCurrentCommand()
    {
        _commandProcessor.CancelCurrentCommand();
    }

    public void ClearCommands()
    {
        _commandProcessor.ClearCommands();
    }
    public void ResetCancellation()
    {
        _commandProcessor.ResetCancellation();
    }

    public abstract void Enter();
    public abstract void Exit();
    public virtual void Update()
    {
        if (!_commandProcessor._isProcessing)
        {
            _commandProcessor.ProcessCommandsAsync().Forget();
        }
    }

    public virtual void Dispose()
    {
        _commandProcessor.ClearCommands();
        _commandProcessor.ResetCancellation();
    }
}