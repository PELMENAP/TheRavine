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
        public string Name => "-tp";
        public string Description => "Телепортирует игрока: -tp i x y";

        public bool Validate(CommandContext context)
        {
            if (context.PlayerData == null)
            {
                context.Display("Игрок ещё не инициализирован");
                return false;
            }
            return true;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext ctx)
        {
            if (args.Length < 4 || args[1] != "i")
            {
                ctx.Display("Использование: -tp i x y");
                return UniTask.CompletedTask;
            }
            if (!int.TryParse(args[2], out var x) || !int.TryParse(args[3], out var y))
            {
                ctx.Display("Неизвестный тип координат");
                return UniTask.CompletedTask;
            }
            if (Mathf.Abs(x) > 1000000 || Mathf.Abs(y) > 1000000)
            {
                ctx.Display("Превышен лимит мира");
                return UniTask.CompletedTask;
            }
            ctx.PlayerData.GetEntityComponent<TransformComponent>().GetEntityTransform().position = new Vector2(x, y);
            ctx.Display($"Выполнен телепорт на координаты: {x}, {y}");
            return UniTask.CompletedTask;
        }
    }
    public abstract class SetValueCommandBase : IValidatedCommand
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        protected abstract void Apply(PlayerEntity player, int value, CommandContext ctx);

        public bool Validate(CommandContext context)
        {
            if (context.PlayerData == null)
            {
                context.Display("Игрок ещё не инициализирован");
                return false;
            }
            return true;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext ctx)
        {
            if (args.Length < 4 || args[1] != "i")
            {
                ctx.Display($"Использование: {Name} i <значение>");
                return UniTask.CompletedTask;
            }
            if (!int.TryParse(args[3], out var val) || val < 0)
            {
                ctx.Display("Недопустимое значение");
                return UniTask.CompletedTask;
            }
            Apply(ctx.PlayerData, val, ctx);
            return UniTask.CompletedTask;
        }
    }

    public class SetSpeedCommand : SetValueCommandBase
    {
        public override string Name => "-set-speed";
        public override string Description => "Устанавливает скорость игрока: -set-speed i <0..100>";

        protected override void Apply(PlayerEntity player, int value, CommandContext ctx)
        {
            if (value > 100)
            {
                ctx.Display("Превышен лимит скорости");
                return;
            }
            player.GetEntityComponent<MovementComponent>().baseStats.baseSpeed = value;
            ctx.Display($"Скорость игрока установлена: {value}");
        }
    }

    public class SetViewCommand : SetValueCommandBase
    {
        public override string Name => "-set-view";
        public override string Description => "Устанавливает обзор игрока: -set-view i <0..30>";

        protected override void Apply(PlayerEntity player, int value, CommandContext ctx)
        {
            if (value > 30)
            {
                ctx.Display("Превышен лимит обзора");
                return;
            }
            player.GetEntityComponent<AimComponent>().BaseStats.crosshairDistance = value;
            ctx.Display($"Обзор игрока установлен: {value}");
        }
    }
}