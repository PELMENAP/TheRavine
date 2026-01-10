using System;
using System.Collections.Generic;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;


using TheRavine.Generator;
using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class TeleportCommand : IValidatedCommand
    {
        public string Name => "~tp";
        public string ShortName => "~tp";
        public string Description => "Телепортирует игрока: ~tp i x y";

        public bool Validate(CommandContext context)
        {
            if (context.PlayersData == null || context.PlayersData.Count < 1)
            {
                context.Display("Игрок ещё не инициализирован");
                return false;
            }
            return true;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 4 || args[1] != "i")
            {
                context.Display("Использование: ~tp i x y");
                return UniTask.CompletedTask;
            }
            if (!int.TryParse(args[2], out var x) || !int.TryParse(args[3], out var y))
            {
                context.Display("Неизвестный тип координат");
                return UniTask.CompletedTask;
            }
            if (Mathf.Abs(x) > 1000000 || Mathf.Abs(y) > 1000000)
            {
                context.Display("Превышен лимит мира");
                return UniTask.CompletedTask;
            }
            context.PlayersData[0].GetEntityComponent<TransformComponent>().GetEntityTransform().position = new Vector2(x, y);
            context.Display($"Выполнен телепорт на координаты: {x}, {y}");
            return UniTask.CompletedTask;
        }
    }
    public class SetValueCommand : IValidatedCommand
    {
        public string Name => "~set";
        public string ShortName => "~s";
        public string Description => "Устанавливает числовые параметры игрока: ~set i [parameter] <0..100>";
        public bool Validate(CommandContext context)
        {
            if (context.PlayersData == null || context.PlayersData.Count < 1)
            {
                context.Display("Игрок ещё не инициализирован");
                return false;
            }
            return true;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 4 || args[1] != "i")
            {
                context.Display($"Использование: {Name} i <значение>");
                return UniTask.CompletedTask;
            }
            if (!int.TryParse(args[3], out var val) || val < 0)
            {
                context.Display("Недопустимое значение");
                return UniTask.CompletedTask;
            }

            switch (args[2])
            {
                case "speed":
                    ApplySpeed(context.PlayersData[0], val, context);
                    break;
                default:
                    context.Display("Неизвестный параметр");
                    break;
            }

            return UniTask.CompletedTask;
        }

        private void ApplySpeed(AEntity player, int value, CommandContext context)
        {
            if (value > 100)
            {
                context.Display("Превышен лимит скорости");
                return;
            }
            player.GetEntityComponent<MovementComponent>().SetBaseSpeed(value);
            context.Display($"Скорость игрока установлена: {value}");
        }
    }
}