using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public delegate void Behaviour();
public abstract class AState
{
    protected List<ICommand> _commands = new List<ICommand>();
    private ICommand _currentCommand;
    private bool _isProcessing;

    public void AddCommand(ICommand command)
    {
        _commands.Add(command);
    }

    public async UniTask ProcessCommandsAsync()
    {
        _isProcessing = true;
        while (_commands.Count > 0 && _isProcessing)
        {
            _currentCommand = _commands[0];
            _commands.RemoveAt(0);
            await _currentCommand.ExecuteAsync();
            if (!_isProcessing)
                _currentCommand.Cancel();
        }
        _currentCommand = null;
    }

    public void CancelCurrentCommand()
    {
        _isProcessing = false;
        _currentCommand?.Cancel();
    }
    public void ClearCommands()
    {
        if (_isProcessing && _currentCommand != null)
            CancelCurrentCommand();
        _commands.Clear();
    }
    public abstract void Enter();
    public abstract void Exit();
    public abstract void Update();
}