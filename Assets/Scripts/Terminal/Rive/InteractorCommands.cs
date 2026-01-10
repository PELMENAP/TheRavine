using System;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class InteractorListCommand : ICommand
    {
        public string Name => "~interactors";
        public string ShortName => "~int";
        public string Description => "Показывает список доступных интеракторов";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            await UniTask.Yield();
            
            var interactors = context.ScriptInterpreter.GetRegisteredInteractors();
            
            if (interactors.Count == 0)
            {
                context.Display("Нет зарегистрированных интеракторов");
                return;
            }
            
            context.Display($"Доступные интеракторы ({interactors.Count}):");
            foreach (var name in interactors)
            {
                var interactor = context.ScriptInterpreter.GetInteractor(name);
                if (interactor != null)
                {
                    context.Display($"  [{name}] - {interactor.Description}");
                }
            }
        }
    }

    public class InteractorResetCommand : ICommand
    {
        public string Name => "~reset";
        public string ShortName => "~rst";
        public string Description => "Сбрасывает состояние интерактора: ~reset <interactor_name>";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            await UniTask.Yield();
            
            if (args.Length < 2)
            {
                context.Display("Использование: ~reset <interactor_name>");
                return;
            }
            
            var interactorName = args[1];
            var interactor = context.ScriptInterpreter.GetInteractor(interactorName);
            
            if (interactor == null)
            {
                context.Display($"Интерактор '{interactorName}' не найден");
                return;
            }
            
            interactor.Reset();
            context.Display($"Интерактор '{interactorName}' сброшен");
        }
    }

    public class InputCommand : ICommand
    {
        public string Name => "~input";
        public string ShortName => "~in";
        public string Description => "Отправляет значение в поток ввода: ~input <value>";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            await UniTask.Yield();
            
            if (args.Length < 2)
            {
                context.Display("Использование: ~input <value>");
                return;
            }
            
            if (!int.TryParse(args[1], out int value))
            {
                context.Display($"Неверный формат числа: {args[1]}");
                return;
            }
            
            context.ScriptInterpreter.PushInput(value);
            
            var waiting = context.ScriptInterpreter.GetWaitingInputReaders();
            context.Display($"Значение {value} отправлено. Ожидают: {waiting} программ(ы)");
        }
    }
}