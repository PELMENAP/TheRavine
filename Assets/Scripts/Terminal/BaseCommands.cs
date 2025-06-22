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
            foreach (var cmd in context.CommandManager.ListCommands())
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

    public class DebugCommand : ICommand
    {
        public string Name => "-debug";
        public string Description => "Показывает информацию о FPS, Памяти и системе";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display($"Использование: {Name} i <on> / <off>");
                return UniTask.CompletedTask;
            }

            if (args[1] == "on")
            {
                context.Graphy.SetActive(true);
            }
            else if (args[1] == "off")
            {
                context.Graphy.SetActive(false);
            }
            else
            {
                context.Display($"Добавьте <on> / <off>");
            }
            return UniTask.CompletedTask;
        }
    }
    

    public class PrintCommand : ICommand
    {
        public string Name => "-print";
        public string Description => "Печатает строку в терминале";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                return UniTask.CompletedTask;
            }
            var message = string.Join(" ", args, 1, args.Length - 1);
            context.Display(message);
            
            return UniTask.CompletedTask;
        }
    }
}