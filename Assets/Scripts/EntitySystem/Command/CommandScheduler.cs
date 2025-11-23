using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

public class CommandScheduler
{
    private Queue<ICommand> _commands = new Queue<ICommand>();
    private ICommand _currentCommand;
    public bool _isProcessing { get; private set; }
    private CancellationTokenSource _cts = new CancellationTokenSource();
    private ICommand _defaultCommand;
    public void AddCommand(ICommand command)
    {
        _commands.Enqueue(command);
    }
    public void SetDefaultCommand(ICommand defaultCommand)
    {
        _defaultCommand = defaultCommand;
    }

    public async UniTask ProcessCommandsAsync()
    {
        _isProcessing = true;
        while (_isProcessing && !_cts.IsCancellationRequested)
        {
            if (_commands.Count > 0)
            {
                _currentCommand = _commands.Dequeue();
                await _currentCommand.ExecuteAsync().AttachExternalCancellation(_cts.Token);
            }
            else if (_defaultCommand != null)
            {
                await _defaultCommand.ExecuteAsync().AttachExternalCancellation(_cts.Token);
            }
            else
            {
                await UniTask.Yield();
            }
        }
        _currentCommand = null;
    }

    public void CancelCurrentCommand()
    {
        _cts.Cancel();
        _isProcessing = false;
    }

    public void ClearCommands()
    {
        _commands.Clear();
        if (_isProcessing)
            CancelCurrentCommand();
    }

    public void ResetCancellation()
    {
        _cts = new CancellationTokenSource();
    }
}
