using System;
using System.Collections.Generic;
using TMPro;
using Cysharp.Threading.Tasks;


using TheRavine.Generator;
using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class RotateCommand : IValidatedCommand
    {
        public string Name => "~rotate";
        public string ShortName => "~rot";
        public string Description => "Поворачивает пространство: ~rotate <90|-90>";

        public bool Validate(CommandContext context)
        {
            if (context.Generator == null)
            {
                context.Display("Генератор мира ещё не инициализирован");
                return false;
            }
            return true;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext ctx)
        {
            if (args.Length < 2 || !(args[1] == "90" || args[1] == "-90"))
            {
                ctx.Display("Использование: ~rotate <90|-90>");
                return UniTask.CompletedTask;
            }
            var angle = sbyte.Parse(args[1]);
            // ctx.Generator.RotateBasis(angle);
            ctx.Display($"Пространство повернуто на {angle}°");
            return UniTask.CompletedTask;
        }
    }
}