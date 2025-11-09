using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ZLinq;

namespace TheRavine.Base
{
    public class RiveRuntime
    {
        private readonly RiveParser _parser = new();
        private readonly Dictionary<string, ProgramNode> _compiledPrograms = new();
        private RiveExecutor _executor;
        
        public delegate UniTask<bool> TerminalCommandDelegate(string command);
        private TerminalCommandDelegate _terminalCommandHandler;
        
        public void Initialize(TerminalCommandDelegate terminalCommandHandler)
        {
            _terminalCommandHandler = terminalCommandHandler;
            _executor = new RiveExecutor(this);
        }
        
        public void LoadFile(string fileName, string content)
        {
            try
            {
                var program = _parser.Parse(fileName, content);
                _compiledPrograms[fileName] = program;
            }
            catch (RiveParseException ex)
            {
                throw new RiveRuntimeException($"Failed to load {fileName}: {ex.Message}", ex);
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
            if (!_compiledPrograms.TryGetValue(fileName, out var program))
            {
                return new ProgramInfo
                {
                    Name = "null",
                    Parameters = new List<string>(),
                    StatementCount = 0,
                    IsLoaded = false
                };
            }
            
            return new ProgramInfo
            {
                Name = program.Name,
                Parameters = program.Parameters,
                StatementCount = program.Statements.Count,
                IsLoaded = true
            };
        }
        public async UniTask<ScriptResult> ExecuteScriptAsync(string fileName, params int[] args)
        {
            if (!_compiledPrograms.TryGetValue(fileName, out var program))
            {
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = $"File {fileName} not found"
                };
            }
            
            var result = await _executor.ExecuteAsync(program, args);
            
            return new ScriptResult
            {
                Success = result.Success,
                ReturnValue = result.Action == ExecutionAction.Return ? result.ReturnValue : 0,
                ErrorMessage = result.ErrorMessage
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
        
        public struct ProgramInfo
        {
            public string Name;
            public List<string> Parameters;
            public int StatementCount;
            public bool IsLoaded;
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