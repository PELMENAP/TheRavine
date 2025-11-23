using System;
using System.Collections.Generic;
using TMPro;

namespace TheRavine.Base
{
    public class CommandManager
    {
        private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ICommand> _shortVariantsCommands = new(StringComparer.OrdinalIgnoreCase);

        public void Register(params ICommand[] commands)
        {
            foreach (var cmd in commands)
            {
                _commands[cmd.Name] = cmd;
                _shortVariantsCommands[cmd.ShortName] = cmd;
            }
        }

        public bool TryGet(string name, out ICommand command)
        {
            if (_commands.TryGetValue(name, out command))
            {
                return true;
            }
            else
            {
                return _shortVariantsCommands.TryGetValue(name, out command);
            }
        }

        public IEnumerable<ICommand> ListCommands() => _commands.Values;
    }
}