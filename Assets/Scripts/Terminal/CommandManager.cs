using System;
using System.Collections.Generic;
using TMPro;

namespace TheRavine.Base
{
    public class CommandManager
    {
        private readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);

        public void Register(params ICommand[] commands)
        {
            foreach (var cmd in commands)
            {
                _commands[cmd.Name] = cmd;
            }
        }

        public bool TryGet(string name, out ICommand command)
        {
            return _commands.TryGetValue(name, out command);
        }

        public IEnumerable<ICommand> ListCommands() => _commands.Values;
    }
}