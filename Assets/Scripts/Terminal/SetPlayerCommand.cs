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
            if (context.PlayerData == null)
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
            context.PlayerData.GetEntityComponent<TransformComponent>().GetEntityTransform().position = new Vector2(x, y);
            context.Display($"Выполнен телепорт на координаты: {x}, {y}");
            return UniTask.CompletedTask;
        }
    }
    public abstract class SetValueCommandBase : IValidatedCommand
    {
        public abstract string Name { get; }
        public abstract string ShortName { get; }
        public abstract string Description { get; }
        protected abstract void Apply(PlayerEntity player, int value, CommandContext context);

        public bool Validate(CommandContext context)
        {
            if (context.PlayerData == null)
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
            Apply(context.PlayerData, val, context);
            return UniTask.CompletedTask;
        }
    }

    public class SetSpeedCommand : SetValueCommandBase
    {
        public override string Name => "~set~speed";
        public override string ShortName => "~s~s";
        public override string Description => "Устанавливает скорость игрока: ~set~speed i <0..100>";

        protected override void Apply(PlayerEntity player, int value, CommandContext context)
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

    public class SetViewCommand : SetValueCommandBase
    {
        public override string Name => "~set~view";
        public override string ShortName => "~s~v";
        public override string Description => "Устанавливает обзор игрока: ~set~view i <0..30>";

        protected override void Apply(PlayerEntity player, int value, CommandContext context)
        {
            if (value > 30)
            {
                context.Display("Превышен лимит обзора");
                return;
            }
            player.GetEntityComponent<AimComponent>().SetCrosshairDistance(value);
            context.Display($"Обзор игрока установлен: {value}");
        }
    }
}