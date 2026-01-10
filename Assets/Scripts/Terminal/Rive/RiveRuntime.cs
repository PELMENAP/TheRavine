using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ZLinq;

namespace TheRavine.Base
{
    public class RiveRuntime
    {
        private readonly RiveParser _parser;
        private readonly IRavineLogger _logger;
        private readonly Dictionary<string, ProgramNode> _compiledPrograms = new();
        private readonly InputStreamManager _inputStream;
        private readonly InteractorRegistry _interactorRegistry;
        private RiveExecutor _executor;
        
        public delegate UniTask<bool> TerminalCommandDelegate(string command);
        private TerminalCommandDelegate _terminalCommandHandler;
        
        public RiveRuntime(TerminalCommandDelegate terminalCommandHandler, IRavineLogger logger)
        {
            _terminalCommandHandler = terminalCommandHandler;
            _logger = logger;
            _parser = new(logger);
            _inputStream = new InputStreamManager();
            _interactorRegistry = new InteractorRegistry(logger);
            _executor = new RiveExecutor(this, _inputStream, _interactorRegistry);
        }
        
        public void LoadFile(string fileName, string content)
        {
            if (RiveBuiltInFunctions.IsReserved(fileName))
            {
                _logger.LogError($"Cannot load file '{fileName}': this name is reserved");
                throw new RiveRuntimeException($"File name '{fileName}' is reserved");
            }

            try
            {
                var program = _parser.Parse(fileName, content);
                _compiledPrograms[fileName] = program;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load {fileName}: {ex.Message}");
            }
        }
        
        public void UnloadFile(string fileName)
        {
            _compiledPrograms.Remove(fileName);
        }
        
        public bool IsFileLoaded(string fileName)
        {
            return _compiledPrograms.ContainsKey(fileName);
        }

        public List<string> GetLoadedFileNames()
        {
            return _compiledPrograms.Keys.AsValueEnumerable().ToList();
        }
        
        public ProgramInfo GetFileInfo(string fileName)
        {
            if (RiveBuiltInFunctions.IsBuiltIn(fileName))
            {
                return new ProgramInfo
                {
                    Name = fileName,
                    Parameters = new List<string> { "..." },
                    StatementCount = 0,
                    IsLoaded = true,
                    IsBuiltIn = true
                };
            }

            if (!_compiledPrograms.TryGetValue(fileName, out var program))
            {
                return new ProgramInfo
                {
                    Name = "null",
                    Parameters = new List<string>(),
                    StatementCount = 0,
                    IsLoaded = false,
                    IsBuiltIn = false
                };
            }
            
            return new ProgramInfo
            {
                Name = program.Name,
                Parameters = program.Parameters,
                StatementCount = program.Statements.Count,
                IsLoaded = true,
                IsBuiltIn = false
            };
        }

        public async UniTask<ScriptResult> ExecuteScriptAsync(string fileName, params int[] args)
        {
            if (RiveBuiltInFunctions.IsBuiltIn(fileName))
            {
                try
                {
                    var result = await RiveBuiltInFunctions.CallAsync(fileName, args);
                    return new ScriptResult
                    {
                        Success = true,
                        ReturnValue = result,
                        ErrorMessage = null
                    };
                }
                catch (Exception ex)
                {
                    return new ScriptResult
                    {
                        Success = false,
                        ReturnValue = 0,
                        ErrorMessage = ex.Message
                    };
                }
            }

            if (!_compiledPrograms.TryGetValue(fileName, out var program))
            {
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = $"File {fileName} not found"
                };
            }
            
            var execResult = await _executor.ExecuteAsync(program, args);
            
            return new ScriptResult
            {
                Success = execResult.Success,
                ReturnValue = execResult.Action == ExecutionAction.Return ? execResult.ReturnValue : 0,
                ErrorMessage = execResult.ErrorMessage
            };
        }

        internal async UniTask<bool> ExecuteTerminalCommandAsync(string command)
        {
            if (_terminalCommandHandler == null)
                return false;
            
            return await _terminalCommandHandler(command);
        }

        internal async UniTask<ScriptResult> CallFunctionAsync(string functionName, params int[] args)
        {
            return await ExecuteScriptAsync(functionName, args);
        }

        public IReadOnlyCollection<string> GetBuiltInFunctionNames()
        {
            return RiveBuiltInFunctions.GetFunctionNames();
        }

        public void PushInput(int value)
        {
            _inputStream.PushInput(value);
        }

        public int GetWaitingInputReaders()
        {
            return _inputStream.GetWaitingReadersCount();
        }

        public void RegisterInteractor(IInteractor interactor)
        {
            _interactorRegistry.Register(interactor);
        }

        public void UnregisterInteractor(string name)
        {
            _interactorRegistry.Unregister(name);
        }

        public IReadOnlyCollection<string> GetRegisteredInteractors()
        {
            return _interactorRegistry.GetRegisteredNames();
        }

        public IInteractor GetInteractor(string name)
        {
            return _interactorRegistry.Get(name);
        }
        
        public struct ProgramInfo
        {
            public string Name;
            public List<string> Parameters;
            public int StatementCount;
            public bool IsLoaded;
            public bool IsBuiltIn;
        }
        
        public struct ScriptResult
        {
            public bool Success;
            public int ReturnValue;
            public string ErrorMessage;
        }
    }
    
    public class RiveRuntimeException : Exception
    {
        public RiveRuntimeException(string message) : base(message) { }
        public RiveRuntimeException(string message, Exception inner) : base(message, inner) { }
    }
}