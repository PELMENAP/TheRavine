using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using ZLinq;

namespace TheRavine.Base
{
    public class RiveInterpreter
    {
        private Dictionary<string, int> variables = new Dictionary<string, int>();
        private Dictionary<string, GameScriptFile> loadedFiles = new Dictionary<string, GameScriptFile>();
        private int operationCount = 0;
        private const int MAX_OPERATIONS = 1000;

        public GameScriptFile GetFileInfo(string fileName)
        {
            if(loadedFiles.TryGetValue(fileName, out var file))
            {
                return file;
            }
            return new GameScriptFile
            {
                Name = "null", 
                Content = "no content", 
                Parameters = null,
                Lines = null
            };
        }

        public delegate UniTask<bool> TerminalCommandDelegate(string command);
        private TerminalCommandDelegate executeTerminalCommand;

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

        public void Initialize(TerminalCommandDelegate terminalCommandDelegate)
        {
            executeTerminalCommand = terminalCommandDelegate;
        }

        public GameScriptFile ParseScript(string fileName, string content)
        {
            var file = new GameScriptFile
            {
                Name = fileName,
                Content = content
            };

            var lines = content.Split('\n').AsValueEnumerable()
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();
            
            if (lines.Count > 0)
            {
                // Парсинг параметров из первой строки
                var firstLine = lines[0];
                if (firstLine.StartsWith("(") && firstLine.EndsWith(")"))
                {
                    var paramStr = firstLine.Substring(1, firstLine.Length - 2);
                    if (!string.IsNullOrEmpty(paramStr))
                    {
                        file.Parameters = paramStr.Split(',').AsValueEnumerable()
                            .Select(p => p.Trim())
                            .ToList();
                    }
                    lines.RemoveAt(0);
                }
            }

            file.Lines = lines;
            return file;
        }

        // Выполнение скрипта
        public async UniTask<ScriptResult> ExecuteScriptAsync(string fileName, params int[] args)
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
                return await ExecuteLinesAsync(file.Lines, 0);
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

        private async UniTask<ScriptResult> ExecuteLinesAsync(List<string> lines, int startIndex)
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

                var result = await ExecuteLineAsync(line, lines, i);
                if (!result.Success || result.ReturnValue != int.MinValue)
                {
                    return result;
                }

                // Обновляем индекс, если он был изменен (для циклов и условий)
                if (result.ReturnValue == int.MaxValue) // Специальное значение для пропуска индексов
                {
                    i = (int)result.ErrorMessage.GetHashCode(); // Hack для передачи нового индекса
                }

                await UniTask.WaitForEndOfFrame();
            }

            return new ScriptResult { Success = true, ReturnValue = 0 };
        }

        private async UniTask<ScriptResult> ExecuteLineAsync(string line, List<string> allLines, int currentIndex)
        {
            operationCount++;

            // Return statement
            if (line.StartsWith(">>"))
            {
                var expression = line.Substring(2).Trim();
                var value = await EvaluateExpressionAsync(expression);
                return new ScriptResult { Success = true, ReturnValue = value };
            }

            // Variable declaration
            if (line.StartsWith("int "))
            {
                return await HandleVariableDeclarationAsync(line);
            }

            // If statement
            if (line.StartsWith("if "))
            {
                return await HandleIfStatementAsync(line, allLines, currentIndex);
            }

            // For loop
            if (line.StartsWith("for "))
            {
                return await HandleForLoopAsync(line, allLines, currentIndex);
            }

            // Terminal command (строки, начинающиеся с "-")
            if (line.StartsWith("-"))
            {
                return await HandleTerminalCommandAsync(line);
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

        private async UniTask<ScriptResult> HandleVariableDeclarationAsync(string line)
        {
            // int x = expression
            var match = Regex.Match(line, @"int\s+(\w+)\s*=\s*(.+)");
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                var expression = match.Groups[2].Value;
                var value = await EvaluateExpressionAsync(expression);
                variables[varName] = value;
                return new ScriptResult { Success = true, ReturnValue = int.MinValue };
            }

            return new ScriptResult 
            { 
                Success = false, 
                ErrorMessage = $"Неверный синтаксис объявления переменной: {line}" 
            };
        }

        private async UniTask<ScriptResult> HandleIfStatementAsync(string line, List<string> allLines, int currentIndex)
        {
            // if condition
            var condition = line.Substring(3).Trim();
            var conditionResult = await EvaluateConditionAsync(condition);

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
                var result = await ExecuteLinesAsync(ifLines, 0);
                if (!result.Success || result.ReturnValue != int.MinValue)
                {
                    return result;
                }
            }

            // Возвращаем специальное значение для обновления индекса
            return new ScriptResult 
            { 
                Success = true, 
                ReturnValue = int.MaxValue,
                ErrorMessage = endIndex.ToString() // Hack для передачи нового индекса
            };
        }

        private async UniTask<ScriptResult> HandleForLoopAsync(string line, List<string> allLines, int currentIndex)
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
                var result = await ExecuteLinesAsync(loopLines, 0);
                if (!result.Success || result.ReturnValue != int.MinValue)
                {
                    return result;
                }
            }

            // Возвращаем специальное значение для обновления индекса
            return new ScriptResult 
            { 
                Success = true, 
                ReturnValue = int.MaxValue,
                ErrorMessage = endIndex.ToString() // Hack для передачи нового индекса
            };
        }

        private async UniTask<ScriptResult> HandleTerminalCommandAsync(string line)
        {
            if (executeTerminalCommand == null)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = "Терминальные команды не поддерживаются в данном контексте" 
                };
            }

            // Заменяем переменные в команде их значениями
            var processedCommand = ProcessVariablesInCommand(line);
            
            var success = await executeTerminalCommand(processedCommand);
            
            return new ScriptResult 
            { 
                Success = success, 
                ReturnValue = int.MinValue,
                ErrorMessage = success ? null : $"Ошибка выполнения команды: {processedCommand}"
            };
        }

        private string ProcessVariablesInCommand(string command)
        {
            // Заменяем переменные в команде на их значения
            foreach (var variable in variables)
            {
                command = command.Replace($"{{{variable.Key}}}", variable.Value.ToString());
            }
            return command;
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

        private async UniTask<int> EvaluateExpressionAsync(string expression)
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
                        args.Add(await EvaluateExpressionAsync(arg.Trim()));
                    }
                }

                var result = await ExecuteScriptAsync(funcName, args.ToArray());
                return result.Success ? result.ReturnValue : 0;
            }

            // Simple arithmetic
            var parts = Regex.Split(expression, @"(\+|\-|\*|/)")
                .AsValueEnumerable()
                .Select(p => p.Trim())
                .ToArray();
            
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
                var left = await EvaluateExpressionAsync(parts[0]);
                var op = parts[1];
                var right = await EvaluateExpressionAsync(parts[2]);

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

        private async UniTask<bool> EvaluateConditionAsync(string condition)
        {
            var match = Regex.Match(condition, @"(.+?)\s*(>|<|>=|<=|==|!=)\s*(.+)");
            if (match.Success)
            {
                var left = await EvaluateExpressionAsync(match.Groups[1].Value);
                var op = match.Groups[2].Value;
                var right = await EvaluateExpressionAsync(match.Groups[3].Value);

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

        public List<string> GetLoadedFileNames()
        {
            return loadedFiles.Keys.AsValueEnumerable().ToList();
        }
    }
}