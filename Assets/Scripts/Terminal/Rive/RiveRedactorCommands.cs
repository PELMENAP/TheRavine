using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ZLinq;

namespace TheRavine.Base
{
    public class ExecuteScriptCommand : ICommand
    {
        public string Name => "-execute";
        public string ShortName => "-ex";
        public string Description => "Выполняет скрипт: -execute <filename> [args...]";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -execute <filename> [args...]");
                return;
            }

            var fileName = args[1];
            var scriptArgs = new List<int>();

            for (int i = 2; i < args.Length; i++)
            {
                if (int.TryParse(args[i], out int arg))
                {
                    scriptArgs.Add(arg);
                }
                else
                {
                    context.Display($"Неверный аргумент: {args[i]} (ожидается число)");
                    return;
                }
            }

            bool isExist = await context.scriptFileManager.ExistsAsync(fileName);
            if (!isExist)
            {
                context.Display($"Файл {fileName} не найден");
                return;
            }

            try
            {
                context.Display(String.Join(" ", scriptArgs));
                var result = await context.ScriptInterpreter.ExecuteScriptAsync(fileName, scriptArgs.ToArray());
                
                if (result.Success)
                {
                    context.Display($"{fileName}: {result.ReturnValue}");
                }
                else
                {
                    context.Display($"Ошибка выполнения скрипта {fileName}: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                context.Display($"Критическая ошибка при выполнении скрипта {fileName}: {ex.Message}");
            }
        }
    }

    public class EditorCommand : ICommand
    {
        public string Name => "-editor";
        public string ShortName => "-edr";
        public string Description => "Управляет редактором скриптов: -editor <on/off>";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            await UniTask.Yield();
            if (args.Length < 2)
            {
                var status = context.ScriptEditor.IsEditorActive() ? "включен" : "выключен";
                context.Display($"Редактор скриптов {status}. Использование: -editor <on/off>");
                return;
            }

            var action = args[1].ToLower();

            switch (action)
            {
                case "on":
                    context.ScriptEditor.SetEditorActive(true);
                    context.Display("Редактор скриптов включен");
                    break;
                case "off":
                    context.ScriptEditor.SetEditorActive(false);
                    context.Display("Редактор скриптов выключен");
                    break;
                default:
                    context.Display("Использование: -editor <on/off>");
                    break;
            }

            return;
        }
    }

    public class EditFileCommand : ICommand
    {
        public string Name => "-edit";
        public string ShortName => "-edt";
        public string Description => "Открывает файл для редактирования: -edit <filename>";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -edit <filename>");
                return;
            }

            var fileName = args[1];

            if (!context.ScriptEditor.IsEditorActive())
            {
                context.ScriptEditor.SetEditorActive(true);
            }

            bool isExist = await context.scriptFileManager.ExistsAsync(fileName);
            if (isExist)
            {
                context.ScriptEditor.LoadFile(fileName).Forget();
                context.Display($"Файл {fileName} загружен для редактирования");
            }
            else
            {
                context.ScriptEditor.CreateNewFile(fileName).Forget();
                context.Display($"Создан новый файл {fileName} для редактирования");
            }

            return;
        }
    }

    public class ScriptInfoCommand : ICommand
    {
        public string Name => "-file";
        public string ShortName => "-f";
        public string Description => "Показывает информацию о скриптах: -file [list/info <filename>]";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length == 1)
            {
                var files = await context.scriptFileManager.ListIdsAsync();
                context.Display($"Доступно скриптов: {files.Count}");
                context.Display("Используйте: -file list для списка файлов");
                return;
            }

            var action = args[1].ToLower();

            switch (action)
            {
                case "list":
                    var files = await context.scriptFileManager.ListIdsAsync();
                    if (files.Count == 0)
                    {
                        context.Display("Нет сохраненных скриптов");
                    }
                    else
                    {
                        context.Display("Список скриптов:");
                        foreach (var file in files)
                        {
                            context.Display($"  - {file}");
                        }
                    }
                    break;

                case "info":
                    if (args.Length < 3)
                    {
                        context.Display("Использование: -file info <filename>");
                        return;
                    }

                    var fileName = args[2];
                    bool isExist = await context.scriptFileManager.ExistsAsync(fileName);
                    if (!isExist)
                    {
                        context.Display($"Файл {fileName} не найден");
                        return;
                    }

                    var content = await context.scriptFileManager.LoadAsync(fileName);
                    var lines = content?.Split('\n').Length ?? 0;
                    context.Display($"Файл: {fileName}");
                    context.Display($"Строк: {lines}");

                    var fileInfo = context.ScriptInterpreter.GetFileInfo(fileName);
                    if (!fileInfo.IsLoaded && fileInfo.Parameters.Count > 0)
                    {
                        context.Display($"Параметры: ({string.Join(", ", fileInfo.Parameters)})");
                    }
                    break;

                default:
                    context.Display("Использование: -file [list/info <filename>]");
                    break;
            }

            return;
        }
    }
    
    public class DeleteScriptCommand : ICommand
    {
        public string Name => "-delete";
        public string ShortName => "-del";
        public string Description => "Удаляет что-то: -delete [file <filename> / ]";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 3)
            {
                context.Display("Использование: -delete [file <filename> / ]");
                return;
            }

            var action = args[1].ToLower();

            switch (action)
            {
                case "file":
                    var fileName = args[2];
                    bool isExist = await context.scriptFileManager.ExistsAsync(fileName);
                    if (!isExist)
                    {
                        context.Display($"Файл {fileName} не найден");
                        return;
                    }
                    await context.scriptFileManager.DeleteAsync(fileName);
                    context.ScriptInterpreter.UnloadFile(fileName);

                    if (context.ScriptEditor.GetCurrentFileName() == fileName)
                    {
                        context.ScriptEditor.ClearEditor();
                    }

                    context.Display($"Файл {fileName} удален");
                    break;
                default:
                    context.Display("Использование: -delete [file <filename> / ]");
                    break;
            }
            return;
        }
    }

    public class SaveScriptCommand : ICommand
    {
        public string Name => "-save";
        public string ShortName => "-sv";
        public string Description => "Сохраняет текущий файл в редакторе: -save";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (!context.ScriptEditor.IsEditorActive())
            {
                context.Display("Редактор не активен. Используйте -editor on");
                return;
            }

            if (string.IsNullOrEmpty(context.ScriptEditor.GetCurrentFileName()))
            {
                context.Display("Нет открытого файла для сохранения");
                return;
            }

            try
            {
                var content = context.ScriptEditor.GetCurrentContent();

                await context.scriptFileManager.SaveAsync(context.ScriptEditor.GetCurrentFileName(), content);
                context.ScriptInterpreter.LoadFile(context.ScriptEditor.GetCurrentFileName(), content);
                context.Display("Файл сохранен успешно");
            }
            catch (Exception ex)
            {
                context.Display($"Ошибка сохранения: {ex.Message}");
            }

            return;
        }
    }
    public class NewScriptCommand : ICommand
    {
        public string Name => "-new";
        public string ShortName => "-n";
        public string Description => "Создает новый скрипт: -new <filename>";

        public async UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -new <filename>");
                return;
            }

            var fileName = args[1];

            bool isExist = await context.scriptFileManager.ExistsAsync(fileName);
            if (isExist)
            {
                context.Display($"Файл {fileName} уже существует. Используйте -edit {fileName} для редактирования");
                return;
            }

            if (!context.ScriptEditor.IsEditorActive())
            {
                context.ScriptEditor.SetEditorActive(true);
            }

            context.ScriptEditor.CreateNewFile(fileName).Forget();
            context.Display($"Создан новый файл {fileName}");
            
            return;
        }
    }

    public class CloseScriptCommand : ICommand
    {
        public string Name => "-close";
        public string ShortName => "-cs";
        public string Description => "Закрывает текущий файл в редакторе: -close";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (!context.ScriptEditor.IsEditorActive())
            {
                context.Display("Редактор не активен");
                return UniTask.CompletedTask;
            }

            if (string.IsNullOrEmpty(context.ScriptEditor.GetCurrentFileName()))
            {
                context.Display("Нет открытого файла");
                return UniTask.CompletedTask;
            }

            var fileName = context.ScriptEditor.GetCurrentFileName();
            context.ScriptEditor.ClearEditor();
            context.Display($"Файл {fileName} закрыт");
            
            return UniTask.CompletedTask;
        }
    }
}