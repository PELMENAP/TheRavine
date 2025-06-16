using System;
using System.Collections.Generic;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TheRavine.Base
{
    public class ExecuteScriptCommand : ICommand
    {
        public string Name => "-execute";
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

            // Парсинг аргументов
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

            if (!ScriptFileManager.FileExists(fileName))
            {
                context.Display($"Файл {fileName} не найден");
                return;
            }

            try
            {
                var result = await context.ScriptInterpreter.ExecuteScriptAsync(fileName, scriptArgs.ToArray());
                
                if (result.Success)
                {
                    context.Display($"Скрипт {fileName} выполнен успешно. Результат: {result.ReturnValue}");
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
        public string Description => "Управляет редактором скриптов: -editor <on/off>";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                var status = context.ScriptEditor.IsEditorActive() ? "включен" : "выключен";
                context.Display($"Редактор скриптов {status}. Использование: -editor <on/off>");
                return UniTask.CompletedTask;
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

            return UniTask.CompletedTask;
        }
    }

    public class EditFileCommand : ICommand
    {
        public string Name => "-edit";
        public string Description => "Открывает файл для редактирования: -edit <filename>";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -edit <filename>");
                return UniTask.CompletedTask;
            }

            var fileName = args[1];

            // Включаем редактор, если он выключен
            if (!context.ScriptEditor.IsEditorActive())
            {
                context.ScriptEditor.SetEditorActive(true);
            }

            // Если файл существует, загружаем его
            if (ScriptFileManager.FileExists(fileName))
            {
                context.ScriptEditor.LoadFile(fileName);
                context.Display($"Файл {fileName} загружен для редактирования");
            }
            else
            {
                // Создаем новый файл
                context.ScriptEditor.CreateNewFile(fileName);
                context.Display($"Создан новый файл {fileName} для редактирования");
            }

            return UniTask.CompletedTask;
        }
    }

    public class ScriptInfoCommand : ICommand
    {
        public string Name => "-scripts";
        public string Description => "Показывает информацию о скриптах: -scripts [list/info <filename>]";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length == 1)
            {
                // Показываем краткую информацию
                var files = ScriptFileManager.GetFilesList();
                context.Display($"Доступно скриптов: {files.Count}");
                context.Display("Используйте: -scripts list для списка файлов");
                return UniTask.CompletedTask;
            }

            var action = args[1].ToLower();

            switch (action)
            {
                case "list":
                    var files = ScriptFileManager.GetFilesList();
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
                        context.Display("Использование: -scripts info <filename>");
                        return UniTask.CompletedTask;
                    }

                    var fileName = args[2];
                    if (!ScriptFileManager.FileExists(fileName))
                    {
                        context.Display($"Файл {fileName} не найден");
                        return UniTask.CompletedTask;
                    }

                    var content = ScriptFileManager.LoadFile(fileName);
                    var lines = content?.Split('\n').Length ?? 0;
                    context.Display($"Файл: {fileName}");
                    context.Display($"Строк: {lines}");

                    var fileInfo = context.ScriptInterpreter.GetFileInfo(fileName);
                    if (fileInfo != null && fileInfo.Parameters.Count > 0)
                    {
                        context.Display($"Параметры: ({string.Join(", ", fileInfo.Parameters)})");
                    }
                    break;

                default:
                    context.Display("Использование: -scripts [list/info <filename>]");
                    break;
            }

            return UniTask.CompletedTask;
        }
    }
    
    public class DeleteScriptCommand : ICommand
    {
        public string Name => "-delete-script";
        public string Description => "Удаляет скрипт: -delete-script <filename>";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -delete-script <filename>");
                return UniTask.CompletedTask;
            }

            var fileName = args[1];
            
            if (!ScriptFileManager.FileExists(fileName))
            {
                context.Display($"Файл {fileName} не найден");
                return UniTask.CompletedTask;
            }
            ScriptFileManager.DeleteFile(fileName);
            context.ScriptInterpreter.UnloadFile(fileName);
            
            if (context.ScriptEditor.GetCurrentFileName() == fileName)
            {
                context.ScriptEditor.ClearEditor();
            }
            
            context.Display($"Файл {fileName} удален");
            return UniTask.CompletedTask;
        }
    }

    public class SaveScriptCommand : ICommand
    {
        public string Name => "-save";
        public string Description => "Сохраняет текущий файл в редакторе: -save";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (!context.ScriptEditor.IsEditorActive())
            {
                context.Display("Редактор не активен. Используйте -editor on");
                return UniTask.CompletedTask;
            }

            if (string.IsNullOrEmpty(context.ScriptEditor.GetCurrentFileName()))
            {
                context.Display("Нет открытого файла для сохранения");
                return UniTask.CompletedTask;
            }

            try
            {
                var content = context.ScriptEditor.GetCurrentContent();
                ScriptFileManager.SaveFile(context.ScriptEditor.GetCurrentFileName(), content);
                context.ScriptInterpreter.LoadFile(context.ScriptEditor.GetCurrentFileName(), content);
                context.Display("Файл сохранен успешно");
            }
            catch (Exception ex)
            {
                context.Display($"Ошибка сохранения: {ex.Message}");
            }

            return UniTask.CompletedTask;
        }
    }

    // Дополнительные команды для удобства работы
    public class NewScriptCommand : ICommand
    {
        public string Name => "-new-script";
        public string Description => "Создает новый скрипт: -new-script <filename>";

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -new-script <filename>");
                return UniTask.CompletedTask;
            }

            var fileName = args[1];
            
            if (ScriptFileManager.FileExists(fileName))
            {
                context.Display($"Файл {fileName} уже существует. Используйте -edit {fileName} для редактирования");
                return UniTask.CompletedTask;
            }

            // Включаем редактор, если он выключен
            if (!context.ScriptEditor.IsEditorActive())
            {
                context.ScriptEditor.SetEditorActive(true);
            }

            context.ScriptEditor.CreateNewFile(fileName);
            context.Display($"Создан новый файл {fileName}");
            
            return UniTask.CompletedTask;
        }
    }

    public class CloseScriptCommand : ICommand
    {
        public string Name => "-close";
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