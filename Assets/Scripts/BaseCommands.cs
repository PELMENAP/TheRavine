using System;
using System.Collections.Generic;
using TMPro;
using Cysharp.Threading.Tasks;

using TheRavine.Generator;
using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class HelpCommand : ICommand
    {
        public string Name => "-help";
        public string Description => "Показывает список команд";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            context.Display("Доступные команды:");
            foreach (var cmd in context.OutputWindow.GetComponent<Terminal>().CommandManager.ListCommands())
            {
                context.Display($"{cmd.Name}: {cmd.Description}");
            }
            return UniTask.CompletedTask;
        }
    }
    public class ClearCommand : ICommand
    {
        public string Name => "-clear";
        public string Description => "Очищает окно терминала";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            context.Clear();
            return UniTask.CompletedTask;
        }
    }
}