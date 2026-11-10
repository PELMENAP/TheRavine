using System.Collections.Generic;
using System.Threading;

namespace TheRavine.Base
{
    public sealed class RiveRun
    {
        public int OperationCount;
        public int CallDepth;
        public int WaitedMilliseconds;
        public string AbortMessage;
        public CancellationToken Token;
    }

    public sealed class RiveFrame
    {
        public readonly RiveRun Run;
        private readonly Stack<Dictionary<string, object>> _scopes = new();

        public RiveFrame(RiveRun run)
        {
            Run = run;
        }

        public void PushScope()
        {
            _scopes.Push(new Dictionary<string, object>());
        }

        public void PopScope()
        {
            if (_scopes.Count > 0)
                _scopes.Pop();
        }

        public void SetVariable(string name, object value)
        {
            foreach (var scope in _scopes)
            {
                if (scope.ContainsKey(name))
                {
                    scope[name] = value;
                    return;
                }
            }

            if (_scopes.Count > 0)
                _scopes.Peek()[name] = value;
        }

        public object GetVariable(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out var value))
                    return value;
            }
            return null;
        }
    }
}
