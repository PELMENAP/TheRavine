using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

public class CommandScheduler
{
    private readonly Queue<ICommand> _commands = new();
    private ICommand _currentCommand;
    public bool _isProcessing { get; private set; }
    private CancellationTokenSource _cts = new();
    private ICommand _defaultCommand;

    public void AddCommand(ICommand command) => _commands.Enqueue(command);

    public void SetDefaultCommand(ICommand defaultCommand) => _defaultCommand = defaultCommand;

    public async UniTask ProcessCommandsAsync()
    {
        _isProcessing = true;
        var token = _cts.Token;

        while (_isProcessing && !token.IsCancellationRequested)
        {
            var next = _commands.Count > 0 ? _commands.Dequeue() : _defaultCommand;
            if (next == null)
            {
                await UniTask.Yield();
                continue;
            }

            _currentCommand = next;
            try
            {
                await next.ExecuteAsync().AttachExternalCancellation(token);
            }
            finally
            {
                if (ReferenceEquals(_currentCommand, next)) _currentCommand = null;
            }
        }
    }

    public void CancelCurrentCommand()
    {
        var current = _currentCommand;
        _currentCommand = null;
        _isProcessing = false;
        _cts.Cancel();
        current?.Cancel();
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