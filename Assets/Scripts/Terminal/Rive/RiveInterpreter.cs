using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using ZLinq;

namespace TheRavine.Base
{
    public class RiveInterpreter
    {
        private static readonly Regex VarDeclarationRegex = new(@"int\s+(\w+)\s*=\s*(.+)", RegexOptions.Compiled);
        private static readonly Regex FunctionCallRegex = new(@"(\w+)\(([^)]*)\)", RegexOptions.Compiled);
        private static readonly Regex ForLoopRegex = new(@"for\s+(\w+)\s*=\s*(\d+)\s+to\s+(\d+)", RegexOptions.Compiled);
        private static readonly Regex ArithmeticRegex = new(@"(\+|\-|\*|/)", RegexOptions.Compiled);
        private static readonly Regex ConditionRegex = new(@"(.+?)\s*(>|<|>=|<=|==|!=)\s*(.+)", RegexOptions.Compiled);
        private static readonly Regex VariableInCommandRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);
        private readonly Stack<Dictionary<string, int>> _variableScopes = new();
        private Dictionary<string, GameScriptFile> _loadedFiles = new();
        private int _operationCount = 0;
        private const int MAX_OPERATIONS = 1000;

        public GameScriptFile GetFileInfo(string fileName)
        {
            return _loadedFiles.TryGetValue(fileName, out var file) ? file : 
                new GameScriptFile { Name = "null", Content = "no content", Parameters = null, Lines = null };
        }

        public delegate UniTask<bool> TerminalCommandDelegate(string command);
        private TerminalCommandDelegate _executeTerminalCommand;
        public struct ExecutionResult
        {
            public bool Success;
            public int ReturnValue;
            public string ErrorMessage;
            public ExecutionAction Action;
            public int JumpIndex;

            public static ExecutionResult CreateSuccess(int returnValue = 0) =>
                new() { Success = true, ReturnValue = returnValue, Action = ExecutionAction.Continue };

            public static ExecutionResult CreateReturn(int value) =>
                new() { Success = true, ReturnValue = value, Action = ExecutionAction.Return };

            public static ExecutionResult CreateJump(int index) =>
                new() { Success = true, Action = ExecutionAction.Jump, JumpIndex = index };

            public static ExecutionResult CreateError(string message) =>
                new() { Success = false, ErrorMessage = message, Action = ExecutionAction.Stop };
        }

        public enum ExecutionAction
        {
            Continue,
            Return,
            Jump,
            Stop
        }

        public class GameScriptFile
        {
            public string Name { get; set; }
            public string Content { get; set; }
            public List<string> Parameters { get; set; } = new();
            public List<string> Lines { get; set; } = new();
        }

        public class ScriptResult
        {
            public bool Success { get; set; }
            public int ReturnValue { get; set; }
            public string ErrorMessage { get; set; }
        }

        public void Initialize(TerminalCommandDelegate terminalCommandDelegate)
        {
            _executeTerminalCommand = terminalCommandDelegate;
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
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("&"))
                .ToList();
            
            if (lines.Count > 0)
            {
                var firstLine = lines[0];
                if (firstLine.StartsWith("(") && firstLine.EndsWith(")"))
                {
                    var paramStr = firstLine.Substring(1, firstLine.Length - 2);
                    if (!string.IsNullOrEmpty(paramStr))
                    {
                        file.Parameters = paramStr.Split(',').AsValueEnumerable()
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrEmpty(p))
                            .ToList();
                    }
                    lines.RemoveAt(0);
                }
            }

            file.Lines = lines;
            return file;
        }

        public async UniTask<ScriptResult> ExecuteScriptAsync(string fileName, params int[] args)
        {
            if (!_loadedFiles.ContainsKey(fileName))
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Файл {fileName} не найден" 
                };
            }

            var file = _loadedFiles[fileName];
            
            if (args.Length != file.Parameters.Count)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Неверное количество аргументов. Ожидается: {file.Parameters.Count}, получено: {args.Length}" 
                };
            }

            _operationCount = 0;
            
            // Создаем новую область видимости
            PushScope();
            
            try
            {
                // Инициализация параметров как переменных в текущей области
                for (int i = 0; i < file.Parameters.Count; i++)
                {
                    SetVariable(file.Parameters[i], args[i]);
                }

                var result = await ExecuteLinesAsync(file.Lines, 0, file.Lines.Count);
                
                return new ScriptResult
                {
                    Success = result.Success,
                    ReturnValue = result.Action == ExecutionAction.Return ? result.ReturnValue : 0,
                    ErrorMessage = result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                return new ScriptResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Ошибка выполнения: {ex.Message}" 
                };
            }
            finally
            {
                PopScope();
            }
        }

        // Избегание копирования списков - работаем с диапазонами
        private async UniTask<ExecutionResult> ExecuteLinesAsync(List<string> lines, int startIndex, int endIndex)
        {
            for (int i = startIndex; i < endIndex; i++)
            {
                if (_operationCount >= MAX_OPERATIONS)
                {
                    return ExecutionResult.CreateError($"Превышен лимит операций ({MAX_OPERATIONS})");
                }

                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var result = await ExecuteLineAsync(line, lines, i, endIndex);
                
                switch (result.Action)
                {
                    case ExecutionAction.Return:
                        return result;
                    case ExecutionAction.Stop:
                        return result;
                    case ExecutionAction.Jump:
                        i = result.JumpIndex;
                        break;
                    case ExecutionAction.Continue:
                        break;
                }

                await UniTask.WaitForEndOfFrame();
            }

            return ExecutionResult.CreateSuccess();
        }

        private async UniTask<ExecutionResult> ExecuteLineAsync(string line, List<string> allLines, int currentIndex, int blockEndIndex)
        {
            _operationCount++;

            if (line.StartsWith(">>")) // Return statement
            {
                var expression = line.Substring(2).Trim();
                var value = await EvaluateExpressionAsync(expression);
                return ExecutionResult.CreateReturn(value);
            }
            if (line.StartsWith("int ")) // Variable declaration
            {
                return await HandleVariableDeclarationAsync(line);
            }
            if (line.StartsWith("if ")) // If statement with else support
            {
                return await HandleIfStatementAsync(line, allLines, currentIndex, blockEndIndex);
            }
            if (line.StartsWith("for ")) // For loop
            {
                return await HandleForLoopAsync(line, allLines, currentIndex, blockEndIndex);
            }
            if (line.StartsWith("-")) // Terminal command
            {
                return await HandleTerminalCommandAsync(line);
            }
            if (line.Contains('=') && !line.StartsWith("int ")) // a = a + n operator
            {
                return await HandleAssignmentAsync(line);
            }
            if (line.StartsWith("log ")) // logging the variable
            {
                return await HandleLogCommandAsync(line);
            }

            // Skip control flow keywords
            if (line == "end" || line == "else")
            {
                return ExecutionResult.CreateSuccess();
            }

            return ExecutionResult.CreateError($"Неизвестная команда: {line}");
        }

        private async UniTask<ExecutionResult> HandleVariableDeclarationAsync(string line)
        {
            var match = VarDeclarationRegex.Match(line);
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                var expression = match.Groups[2].Value;
                var value = await EvaluateExpressionAsync(expression);
                SetVariable(varName, value);
                return ExecutionResult.CreateSuccess();
            }

            return ExecutionResult.CreateError($"Неверный синтаксис объявления переменной: {line}");
        }

        private async UniTask<ExecutionResult> HandleIfStatementAsync(string line, List<string> allLines, int currentIndex, int blockEndIndex)
        {
            var condition = line.Substring(3).Trim();
            var conditionResult = await EvaluateConditionAsync(condition);

            var (elseIndex, endIndex) = FindIfBlockBounds(allLines, currentIndex, blockEndIndex);
            
            if (endIndex == -1)
            {
                return ExecutionResult.CreateError("Не найден end для if");
            }

            if (conditionResult)
            {
                // Выполняем блок if
                var ifEndIndex = elseIndex != -1 ? elseIndex : endIndex;
                var result = await ExecuteLinesAsync(allLines, currentIndex + 1, ifEndIndex);
                if (result.Action != ExecutionAction.Continue)
                {
                    return result;
                }
            }
            else if (elseIndex != -1)
            {
                // Выполняем блок else
                var result = await ExecuteLinesAsync(allLines, elseIndex + 1, endIndex);
                if (result.Action != ExecutionAction.Continue)
                {
                    return result;
                }
            }

            return ExecutionResult.CreateJump(endIndex);
        }

        private async UniTask<ExecutionResult> HandleForLoopAsync(string line, List<string> allLines, int currentIndex, int blockEndIndex)
        {
            var match = ForLoopRegex.Match(line);
            if (!match.Success)
            {
                return ExecutionResult.CreateError($"Неверный синтаксис цикла: {line}");
            }

            var varName = match.Groups[1].Value;
            var start = int.Parse(match.Groups[2].Value);
            var end = int.Parse(match.Groups[3].Value);

            var endIndex = FindEndStatement(allLines, currentIndex, blockEndIndex);
            if (endIndex == -1)
            {
                return ExecutionResult.CreateError("Не найден end для for");
            }
            PushScope();
            
            try
            {
                for (int i = start; i <= end; i++)
                {
                    if (_operationCount >= MAX_OPERATIONS)
                    {
                        return ExecutionResult.CreateError("Превышен лимит операций в цикле");
                    }

                    SetVariable(varName, i);
                    var result = await ExecuteLinesAsync(allLines, currentIndex + 1, endIndex);
                    
                    if (result.Action == ExecutionAction.Return || result.Action == ExecutionAction.Stop)
                    {
                        return result;
                    }
                }
            }
            finally
            {
                PopScope();
            }

            return ExecutionResult.CreateJump(endIndex);
        }

        private async UniTask<ExecutionResult> HandleTerminalCommandAsync(string line)
        {
            if (_executeTerminalCommand == null)
            {
                return ExecutionResult.CreateError("Терминальные команды не поддерживаются в данном контексте");
            }

            var processedCommand = ProcessVariablesInCommandSafe(line);
            var success = await _executeTerminalCommand(processedCommand);
            
            return success ? ExecutionResult.CreateSuccess() : 
                ExecutionResult.CreateError($"Ошибка выполнения команды: {processedCommand}");
        }

        private async UniTask<ExecutionResult> HandleAssignmentAsync(string line)
        {
            var parts = line.Split('=');
            if (parts.Length != 2)
                return ExecutionResult.CreateError($"Неверный синтаксис присваивания: {line}");

            var varName = parts[0].Trim();
            var expression = parts[1].Trim();

            var value = await EvaluateExpressionAsync(expression);

            if (GetVariable(varName) == null)
                return ExecutionResult.CreateError($"Переменная {varName} не объявлена");

            SetVariable(varName, value);
            return ExecutionResult.CreateSuccess();
        }
        private async UniTask<ExecutionResult> HandleLogCommandAsync(string line)
        {
            var expression = line.Substring(4).Trim();
            
            if (string.IsNullOrEmpty(expression))
            {
                return new ExecutionResult 
                { 
                    Success = false, 
                    ErrorMessage = "Команда log требует аргумент" 
                };
            }

            try
            {
                var value = await EvaluateExpressionAsync(expression);
                
                if (_executeTerminalCommand != null)
                {
                    await _executeTerminalCommand($"-print {expression} = {value}");
                }
                
                return new ExecutionResult { Success = true, ReturnValue = int.MinValue };
            }
            catch (Exception ex)
            {
                return new ExecutionResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Ошибка при вычислении выражения '{expression}': {ex.Message}" 
                };
            }
        }
        private string ProcessVariablesInCommandSafe(string command)
        {
            return VariableInCommandRegex.Replace(command, match =>
            {
                var varName = match.Groups[1].Value;
                return GetVariable(varName)?.ToString() ?? match.Value;
            });
        }
        private (int elseIndex, int endIndex) FindIfBlockBounds(List<string> lines, int startIndex, int blockEndIndex)
        {
            int depth = 1;
            int elseIndex = -1;
            
            for (int i = startIndex + 1; i < blockEndIndex && i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("if ") || line.StartsWith("for "))
                {
                    depth++;
                }
                else if (line == "else" && depth == 1 && elseIndex == -1)
                {
                    elseIndex = i;
                }
                else if (line == "end")
                {
                    depth--;
                    if (depth == 0)
                    {
                        return (elseIndex, i);
                    }
                }
            }
            return (-1, -1);
        }

        private int FindEndStatement(List<string> lines, int startIndex, int blockEndIndex)
        {
            int depth = 1;
            for (int i = startIndex + 1; i < blockEndIndex && i < lines.Count; i++)
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
            var funcMatch = FunctionCallRegex.Match(expression);
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
            return EvaluateSimpleExpression(expression);
        }

        private int EvaluateSimpleExpression(string expression)
        {
            expression = expression.Replace(" ", "");

            var parts = ArithmeticRegex.Split(expression);
            
            if (parts.Length == 1)
            {
                if (int.TryParse(parts[0], out int num))
                    return num;
                return GetVariable(parts[0]) ?? 0;
            }

            if (parts.Length == 3)
            {
                var left = EvaluateSimpleExpression(parts[0]);
                var op = parts[1];
                var right = EvaluateSimpleExpression(parts[2]);

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
            var match = ConditionRegex.Match(condition);
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

        private void PushScope()
        {
            _variableScopes.Push(new Dictionary<string, int>());
        }

        private void PopScope()
        {
            if (_variableScopes.Count > 0)
                _variableScopes.Pop();
        }

        private void SetVariable(string name, int value)
        {
            foreach (var scope in _variableScopes)
            {
                if (scope.ContainsKey(name))
                {
                    scope[name] = value;
                    return;
                }
            }
            if (_variableScopes.Count > 0)
            {
                _variableScopes.Peek()[name] = value;
            }
        }

        private int? GetVariable(string name)
        {
            foreach (var scope in _variableScopes)
            {
                if (scope.TryGetValue(name, out var value))
                    return value;
            }
            return null;
        }

        public void LoadFile(string fileName, string content)
        {
            var file = ParseScript(fileName, content);
            _loadedFiles[fileName] = file;
        }

        public void UnloadFile(string fileName)
        {
            _loadedFiles.Remove(fileName);
        }

        public bool IsFileLoaded(string fileName)
        {
            return _loadedFiles.ContainsKey(fileName);
        }

        public List<string> GetLoadedFileNames()
        {
            return _loadedFiles.Keys.AsValueEnumerable().ToList();
        }
    }
}