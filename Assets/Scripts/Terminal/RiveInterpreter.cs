using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZLinq;


namespace TheRavine.Base
{
    public class RiveInterpreter
    {
        private Dictionary<string, int> variables = new Dictionary<string, int>();
        private Dictionary<string, GameScriptFile> loadedFiles = new Dictionary<string, GameScriptFile>();
        private int operationCount = 0;
        private const int MAX_OPERATIONS = 100;

        public class GameScriptFile
        {
            public string Name { get; set; }
            public string Content { get; set; }
            public List<string> Parameters { get; set; } = new List<string>();
            public List<string> Lines { get; set; } = new List<string>();
        }

        public class ScriptResult
        {
            public bool Success { get; set; }
            public int ReturnValue { get; set; }
            public string ErrorMessage { get; set; }
        }

        // Парсинг файла скрипта
        public GameScriptFile ParseScript(string fileName, string content)
        {
            var file = new GameScriptFile
            {
                Name = fileName,
                Content = content
            };

            var lines = content.Split('\n').AsValueEnumerable().Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
            
            if (lines.Count > 0)
            {
                // Парсинг параметров из первой строки
                var firstLine = lines[0];
                if (firstLine.StartsWith("(") && firstLine.EndsWith(")"))
                {
                    var paramStr = firstLine.Substring(1, firstLine.Length - 2);
                    if (!string.IsNullOrEmpty(paramStr))
                    {
                        file.Parameters = paramStr.Split(',').AsValueEnumerable().Select(p => p.Trim()).ToList();
                    }
                    lines.RemoveAt(0);
                }
            }

            file.Lines = lines;
            return file;
        }

        // Выполнение скрипта
        public ScriptResult ExecuteScript(string fileName, params int[] args)
        {
            if (!loadedFiles.ContainsKey(fileName))
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Файл {fileName} не найден" 
                };
            }

            var file = loadedFiles[fileName];
            
            // Проверка количества аргументов
            if (args.Length != file.Parameters.Count)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Неверное количество аргументов. Ожидается: {file.Parameters.Count}, получено: {args.Length}" 
                };
            }

            // Сброс счетчика операций и переменных
            operationCount = 0;
            variables.Clear();

            // Инициализация параметров как переменных
            for (int i = 0; i < file.Parameters.Count; i++)
            {
                variables[file.Parameters[i]] = args[i];
            }

            try
            {
                return ExecuteLines(file.Lines, 0);
            }
            catch (Exception ex)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Ошибка выполнения: {ex.Message}" 
                };
            }
        }

        private ScriptResult ExecuteLines(List<string> lines, int startIndex)
        {
            for (int i = startIndex; i < lines.Count; i++)
            {
                if (operationCount >= MAX_OPERATIONS)
                {
                    return new ScriptResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Превышен лимит операций (100)" 
                    };
                }

                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var result = ExecuteLine(line, lines, ref i);
                if (!result.Success || result.ReturnValue != int.MinValue)
                {
                    return result;
                }
            }

            return new ScriptResult { Success = true, ReturnValue = 0 };
        }

        private ScriptResult ExecuteLine(string line, List<string> allLines, ref int currentIndex)
        {
            operationCount++;

            // Return statement
            if (line.StartsWith(">>"))
            {
                var expression = line.Substring(2).Trim();
                var value = EvaluateExpression(expression);
                return new ScriptResult { Success = true, ReturnValue = value };
            }

            // Variable declaration
            if (line.StartsWith("int "))
            {
                return HandleVariableDeclaration(line);
            }

            // If statement
            if (line.StartsWith("if "))
            {
                return HandleIfStatement(line, allLines, ref currentIndex);
            }

            // For loop
            if (line.StartsWith("for "))
            {
                return HandleForLoop(line, allLines, ref currentIndex);
            }

            // Terminal command
            if (line.StartsWith("set "))
            {
                HandleTerminalCommand(line);
                return new ScriptResult { Success = true, ReturnValue = int.MinValue };
            }

            // Skip end statements
            if (line == "end")
            {
                return new ScriptResult { Success = true, ReturnValue = int.MinValue };
            }

            return new ScriptResult 
            { 
                Success = false, 
                ErrorMessage = $"Неизвестная команда: {line}" 
            };
        }

        private ScriptResult HandleVariableDeclaration(string line)
        {
            // int x = expression
            var match = Regex.Match(line, @"int\s+(\w+)\s*=\s*(.+)");
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                var expression = match.Groups[2].Value;
                var value = EvaluateExpression(expression);
                variables[varName] = value;
                return new ScriptResult { Success = true, ReturnValue = int.MinValue };
            }

            return new ScriptResult 
            { 
                Success = false, 
                ErrorMessage = $"Неверный синтаксис объявления переменной: {line}" 
            };
        }

        private ScriptResult HandleIfStatement(string line, List<string> allLines, ref int currentIndex)
        {
            // if condition
            var condition = line.Substring(3).Trim();
            var conditionResult = EvaluateCondition(condition);

            var endIndex = FindEndStatement(allLines, currentIndex);
            if (endIndex == -1)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = "Не найден end для if" 
                };
            }

            if (conditionResult)
            {
                var ifLines = allLines.GetRange(currentIndex + 1, endIndex - currentIndex - 1);
                var result = ExecuteLines(ifLines, 0);
                if (!result.Success || result.ReturnValue != int.MinValue)
                {
                    return result;
                }
            }

            currentIndex = endIndex;
            return new ScriptResult { Success = true, ReturnValue = int.MinValue };
        }

        private ScriptResult HandleForLoop(string line, List<string> allLines, ref int currentIndex)
        {
            // for i = 1 to 10
            var match = Regex.Match(line, @"for\s+(\w+)\s*=\s*(\d+)\s+to\s+(\d+)");
            if (!match.Success)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Неверный синтаксис цикла: {line}" 
                };
            }

            var varName = match.Groups[1].Value;
            var start = int.Parse(match.Groups[2].Value);
            var end = int.Parse(match.Groups[3].Value);

            var endIndex = FindEndStatement(allLines, currentIndex);
            if (endIndex == -1)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = "Не найден end для for" 
                };
            }

            var loopLines = allLines.GetRange(currentIndex + 1, endIndex - currentIndex - 1);
            
            for (int i = start; i <= end; i++)
            {
                if (operationCount >= MAX_OPERATIONS)
                {
                    return new ScriptResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Превышен лимит операций в цикле" 
                    };
                }

                variables[varName] = i;
                var result = ExecuteLines(loopLines, 0);
                if (!result.Success || result.ReturnValue != int.MinValue)
                {
                    return result;
                }
            }

            currentIndex = endIndex;
            return new ScriptResult { Success = true, ReturnValue = int.MinValue };
        }

        private void HandleTerminalCommand(string line)
        {
            // Интеграция с существующим терминалом
            if (terminalContext != null)
            {
                terminalContext.Display($"Выполнение команды: {line}");
            }
            
            // Пример парсинга команды set i speed value
            var match = Regex.Match(line, @"set\s+(\w+)\s+(\w+)\s+(.+)");
            if (match.Success)
            {
                var target = match.Groups[1].Value;
                var property = match.Groups[2].Value;
                var valueExpression = match.Groups[3].Value;
                var value = EvaluateExpression(valueExpression);
                
                // Интеграция с системой команд терминала
                if (target == "i" && property == "speed" && terminalContext?.PlayerData != null)
                {
                    var player = terminalContext.PlayerData;
                    if (value <= 100 && value >= 0)
                    {
                        player.GetEntityComponent<MovementComponent>().baseStats.baseSpeed = value;
                        if (terminalContext != null)
                            terminalContext.Display($"Скорость игрока установлена: {value}");
                    }
                    else
                    {
                        if (terminalContext != null)
                            terminalContext.Display("Недопустимое значение скорости (0-100)");
                    }
                }
            }
        }

        private int FindEndStatement(List<string> lines, int startIndex)
        {
            int depth = 1;
            for (int i = startIndex + 1; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("if ") || line.StartsWith("for "))
                    depth++;
                else if (line == "end")
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        private int EvaluateExpression(string expression)
        {
            expression = expression.Trim();

            // Function call
            var funcMatch = Regex.Match(expression, @"(\w+)\(([^)]*)\)");
            if (funcMatch.Success)
            {
                var funcName = funcMatch.Groups[1].Value;
                var argsStr = funcMatch.Groups[2].Value;
                
                var args = new List<int>();
                if (!string.IsNullOrEmpty(argsStr))
                {
                    foreach (var arg in argsStr.Split(','))
                    {
                        args.Add(EvaluateExpression(arg.Trim()));
                    }
                }

                var result = ExecuteScript(funcName, args.ToArray());
                return result.Success ? result.ReturnValue : 0;
            }

            // Simple arithmetic
            var parts = Regex.Split(expression, @"(\+|\-|\*|/)").AsValueEnumerable().Select(p => p.Trim()).ToArray();
            
            if (parts.Length == 1)
            {
                // Single value
                if (int.TryParse(parts[0], out int num))
                    return num;
                if (variables.ContainsKey(parts[0]))
                    return variables[parts[0]];
                return 0;
            }

            // Binary operation
            if (parts.Length == 3)
            {
                var left = EvaluateExpression(parts[0]);
                var op = parts[1];
                var right = EvaluateExpression(parts[2]);

                return op switch
                {
                    "+" => left + right,
                    "-" => left - right,
                    "*" => left * right,
                    "/" => right != 0 ? left / right : 0,
                    _ => 0
                };
            }

            return 0;
        }

        private bool EvaluateCondition(string condition)
        {
            var match = Regex.Match(condition, @"(.+?)\s*(>|<|>=|<=|==|!=)\s*(.+)");
            if (match.Success)
            {
                var left = EvaluateExpression(match.Groups[1].Value);
                var op = match.Groups[2].Value;
                var right = EvaluateExpression(match.Groups[3].Value);

                return op switch
                {
                    ">" => left > right,
                    "<" => left < right,
                    ">=" => left >= right,
                    "<=" => left <= right,
                    "==" => left == right,
                    "!=" => left != right,
                    _ => false
                };
            }

            return false;
        }

        public void LoadFile(string fileName, string content)
        {
            var file = ParseScript(fileName, content);
            loadedFiles[fileName] = file;
        }

        public void UnloadFile(string fileName)
        {
            loadedFiles.Remove(fileName);
        }

        public bool IsFileLoaded(string fileName)
        {
            return loadedFiles.ContainsKey(fileName);
        }
    }

    // Система сохранения файлов
    public static class ScriptFileManager
    {
        private const string FILES_LIST_KEY = "script_files_list";
        private const string FILE_CONTENT_PREFIX = "script_file_";

        public static void SaveFile(string fileName, string content)
        {
            // Сохранение содержимого файла
            SaveLoad.SaveEncryptedData($"{FILE_CONTENT_PREFIX}{fileName}", content);
            
            // Обновление списка файлов
            var filesList = GetFilesList();
            if (!filesList.Contains(fileName))
            {
                filesList.Add(fileName);
                SaveLoad.SaveEncryptedData(FILES_LIST_KEY, filesList);
            }
        }

        public static string LoadFile(string fileName)
        {
            try
            {
                return SaveLoad.LoadEncryptedData<string>($"{FILE_CONTENT_PREFIX}{fileName}");
            }
            catch
            {
                return null;
            }
        }

        public static List<string> GetFilesList()
        {
            try
            {
                return SaveLoad.LoadEncryptedData<List<string>>(FILES_LIST_KEY) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void DeleteFile(string fileName)
        {
            PlayerPrefs.DeleteKey($"{FILE_CONTENT_PREFIX}{fileName}");
            
            var filesList = GetFilesList();
            filesList.Remove(fileName);
            SaveLoad.SaveEncryptedData(FILES_LIST_KEY, filesList);
        }

        public static bool FileExists(string fileName)
        {
            return PlayerPrefs.HasKey($"{FILE_CONTENT_PREFIX}{fileName}");
        }
    }
}