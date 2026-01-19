using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public delegate UniTask<object> BuiltInFunction(params object[] args);

    public static class RiveBuiltInFunctions
    {
        private static readonly Dictionary<string, BuiltInFunction> Functions = new()
        {
            ["abs"] = Abs,
            ["min"] = Min,
            ["max"] = Max,
            ["clamp"] = Clamp,
            ["pow"] = Pow,
            ["sqrt"] = Sqrt,
            ["rand"] = Rand,
            ["sum"] = Sum,
            ["avg"] = Avg,
            ["hadamard"] = Hadamard,
            ["measure"] = Measure,
            ["phase"] = Phase,
        };

        private static readonly HashSet<string> ReservedKeywords = new()
        {
            "get", "send"
        };

        public static bool IsBuiltIn(string name) => Functions.ContainsKey(name);

        public static bool IsReservedKeyword(string name) => ReservedKeywords.Contains(name);

        public static bool IsReserved(string name) => IsBuiltIn(name) || IsReservedKeyword(name);

        public static IReadOnlyCollection<string> GetFunctionNames() => Functions.Keys;

        public static async UniTask<object> CallAsync(string name, params object[] args)
        {
            if (!Functions.TryGetValue(name, out var function))
                throw new RiveRuntimeException($"Built-in function '{name}' not found");

            return await function(args);
        }

        private static UniTask<object> Abs(params object[] args)
        {
            if (args.Length != 1 || args[0] is not int val)
                throw new RiveRuntimeException("abs: expected 1 int argument");
            
            return UniTask.FromResult<object>(Math.Abs(val));
        }

        private static UniTask<object> Min(params object[] args)
        {
            if (args.Length < 2)
                throw new RiveRuntimeException("min: expected at least 2 arguments");
            
            int result = args[0] is int i0 ? i0 : 0;
            for (int idx = 1; idx < args.Length; idx++)
            {
                if (args[idx] is int iv && iv < result)
                    result = iv;
            }
            
            return UniTask.FromResult<object>(result);
        }

        private static UniTask<object> Max(params object[] args)
        {
            if (args.Length < 2)
                throw new RiveRuntimeException("max: expected at least 2 arguments");
            
            int result = args[0] is int i0 ? i0 : 0;
            for (int idx = 1; idx < args.Length; idx++)
            {
                if (args[idx] is int iv && iv > result)
                    result = iv;
            }
            
            return UniTask.FromResult<object>(result);
        }

        private static UniTask<object> Clamp(params object[] args)
        {
            if (args.Length != 3 || args[0] is not int value || args[1] is not int min || args[2] is not int max)
                throw new RiveRuntimeException("clamp: expected 3 int arguments (value, min, max)");
            
            if (value < min) return UniTask.FromResult<object>(min);
            if (value > max) return UniTask.FromResult<object>(max);
            return UniTask.FromResult<object>(value);
        }

        private static UniTask<object> Pow(params object[] args)
        {
            if (args.Length != 2 || args[0] is not int baseVal || args[1] is not int exp)
                throw new RiveRuntimeException("pow: expected 2 int arguments (base, exponent)");
            
            if (exp < 0)
                throw new RiveRuntimeException("pow: negative exponents not supported");
            
            int result = 1;
            for (int i = 0; i < exp; i++)
                result *= baseVal;
            
            return UniTask.FromResult<object>(result);
        }

        private static UniTask<object> Sqrt(params object[] args)
        {
            if (args.Length != 1 || args[0] is not int val)
                throw new RiveRuntimeException("sqrt: expected 1 int argument");
            
            if (val < 0)
                throw new RiveRuntimeException("sqrt: negative values not supported");
            
            return UniTask.FromResult<object>((int)Math.Sqrt(val));
        }

        private static UniTask<object> Rand(params object[] args)
        {
            if (args.Length == 0)
                return UniTask.FromResult<object>(UnityEngine.Random.Range(0, int.MaxValue));
            
            if (args.Length == 1 && args[0] is int max)
                return UniTask.FromResult<object>(UnityEngine.Random.Range(0, max));
            
            if (args.Length == 2 && args[0] is int min && args[1] is int max2)
                return UniTask.FromResult<object>(UnityEngine.Random.Range(min, max2));
            
            throw new RiveRuntimeException("rand: expected 0, 1 or 2 int arguments");
        }

        private static UniTask<object> Sum(params object[] args)
        {
            if (args.Length == 0)
                throw new RiveRuntimeException("sum: expected at least 1 argument");
            
            int result = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is int iv)
                    result += iv;
            }
            
            return UniTask.FromResult<object>(result);
        }

        private static UniTask<object> Avg(params object[] args)
        {
            if (args.Length == 0)
                throw new RiveRuntimeException("avg: expected at least 1 argument");
            
            int sum = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is int iv)
                    sum += iv;
            }
            
            return UniTask.FromResult<object>(sum / args.Length);
        }

        private static UniTask<object> Hadamard(params object[] args)
        {
            if (args.Length != 1 || args[0] is not Qbit qbit)
                throw new RiveRuntimeException("hadamard: expected 1 qbit argument");
            
            var result = qbit;
            result.Hadamard();
            return UniTask.FromResult<object>(result);
        }

        private static UniTask<object> Measure(params object[] args)
        {
            if (args.Length != 1 || args[0] is not Qbit qbit)
                throw new RiveRuntimeException("measure: expected 1 qbit argument");
            
            var mutableQbit = qbit;
            return UniTask.FromResult<object>(mutableQbit.Measure());
        }

        private static UniTask<object> Phase(params object[] args)
        {
            if (args.Length != 2 || args[0] is not Qbit qbit || args[1] is not int angleDegrees)
                throw new RiveRuntimeException("phase: expected qbit and int (angle in degrees)");
            
            var result = qbit;
            result.Phase(angleDegrees);
            return UniTask.FromResult<object>(result);
        }
    }
}