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

        private ScriptEditorPresenter scriptEditor;

        public ExecuteScriptCommand(ScriptEditorPresenter editor)
        {
            scriptEditor = editor;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -execute <filename> [args...]");
                return UniTask.CompletedTask;
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
                    context.Display($"Неверный аргument: {args[i]} (ожидается число)");
                    return UniTask.CompletedTask;
                }
            }

            var result = scriptEditor.ExecuteScript(fileName, scriptArgs.ToArray());

            if (result.Success)
            {
                context.Display($"Скрипт {fileName} выполнен успешно. Результат: {result.ReturnValue}");
            }
            else
            {
                context.Display($"Ошибка выполнения скрипта {fileName}: {result.ErrorMessage}");
            }

            return UniTask.CompletedTask;
        }
    }

    public class EditorCommand : ICommand
    {
        public string Name => "-editor";
        public string Description => "Управляет редактором скриптов: -editor <on/off>";

        private ScriptEditorPresenter scriptEditor;

        public EditorCommand(ScriptEditorPresenter editor)
        {
            scriptEditor = editor;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                var status = scriptEditor.IsEditorActive() ? "включен" : "выключен";
                context.Display($"Редактор скриптов {status}. Использование: -editor <on/off>");
                return UniTask.CompletedTask;
            }

            var action = args[1].ToLower();

            switch (action)
            {
                case "on":
                    scriptEditor.SetEditorActive(true);
                    context.Display("Редактор скриптов включен");
                    break;
                case "off":
                    scriptEditor.SetEditorActive(false);
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

        private ScriptEditorPresenter scriptEditor;

        public EditFileCommand(ScriptEditorPresenter editor)
        {
            scriptEditor = editor;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -edit <filename>");
                return UniTask.CompletedTask;
            }

            var fileName = args[1];

            // Включаем редактор, если он выключен
            if (!scriptEditor.IsEditorActive())
            {
                scriptEditor.SetEditorActive(true);
            }

            // Если файл существует, загружаем его
            if (SaveLoad.FileExists(fileName))
            {
                scriptEditor.LoadFile(fileName);
                context.Display($"Файл {fileName} загружен для редактирования");
            }
            else
            {
                // Создаем новый файл
                scriptEditor.CreateNewFile(fileName);
                context.Display($"Создан новый файл {fileName} для редактирования");
            }

            return UniTask.CompletedTask;
        }
    }

    public class ScriptInfoCommand : ICommand
    {
        public string Name => "-scripts";
        public string Description => "Показывает информацию о скриптах: -scripts [list/info <filename>]";

        private ScriptEditorPresenter scriptEditor;

        public ScriptInfoCommand(ScriptEditorPresenter editor)
        {
            scriptEditor = editor;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length == 1)
            {
                // Показываем краткую информацию
                var files = SaveLoad.GetFilesList();
                context.Display($"Доступно скриптов: {files.Count}");
                context.Display("Используйте: -scripts list для списка файлов");
                return UniTask.CompletedTask;
            }

            var action = args[1].ToLower();

            switch (action)
            {
                case "list":
                    var files = SaveLoad.GetFilesList();
                    if (files.Count == 0)
                    {
                        context.Display("Нет сохраненных скриптов");
                    }
                    else
                    {
                        context.Display("Список скриптов:");
                        foreach (var fle in files)
                        {
                            context.Display($"  - {fle}");
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
                    if (!SaveLoad.FileExists(fileName))
                    {
                        context.Display($"Файл {fileName} не найден");
                        return UniTask.CompletedTask;
                    }

                    var content = SaveLoad.SaveEncryptedData<string>(fileName);
                    var lines = content.Split('\n').Length;
                    context.Display($"Файл: {fileName}");
                    context.Display($"Строк: {lines}");

                    var file = new RiveInterpreter().ParseScript(fileName, content);
                    if (file.Parameters.Count > 0)
                    {
                        context.Display($"Параметры: ({string.Join(", ", file.Parameters)})");
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

        private ScriptEditorPresenter scriptEditor;

        public DeleteScriptCommand(ScriptEditorPresenter editor)
        {
            scriptEditor = editor;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (args.Length < 2)
            {
                context.Display("Использование: -delete-script <filename>");
                return UniTask.CompletedTask;
            }

            var fileName = args[1];
            
            if (!SaveLoad.FileExists(fileName))
            {
                context.Display($"Файл {fileName} не найден");
                return UniTask.CompletedTask;
            }

            scriptEditor.DeleteFile(fileName);
            context.Display($"Файл {fileName} удален");

            return UniTask.CompletedTask;
        }
    }

    // Команда для сохранения текущего файла в редакторе
    public class SaveScriptCommand : ICommand
    {
        public string Name => "-save";
        public string Description => "Сохраняет текущий файл в редакторе: -save";

        private ScriptEditorPresenter scriptEditor;

        public SaveScriptCommand(ScriptEditorPresenter editor)
        {
            scriptEditor = editor;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (!scriptEditor.IsEditorActive())
            {
                context.Display("Редактор не активен. Используйте -editor on");
                return UniTask.CompletedTask;
            }

            try
            {
                scriptEditor.SaveCurrentFile();
                context.Display("Файл сохранен успешно");
            }
            catch (System.Exception ex)
            {
                context.Display($"Ошибка сохранения: {ex.Message}");
            }

            return UniTask.CompletedTask;
        }
    }
}