using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public delegate UniTask<int> BuiltInFunction(params int[] args);

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
        };

        private static readonly HashSet<string> ReservedKeywords = new()
        {
            "get", "send"
        };

        public static bool IsBuiltIn(string name) => Functions.ContainsKey(name);

        public static bool IsReservedKeyword(string name) => ReservedKeywords.Contains(name);

        public static bool IsReserved(string name) => IsBuiltIn(name) || IsReservedKeyword(name);

        public static async UniTask<int> CallAsync(string name, params int[] args)
        {
            if (!Functions.TryGetValue(name, out var function))
                throw new RiveRuntimeException($"Built-in function '{name}' not found");

            return await function(args);
        }

        public static IReadOnlyCollection<string> GetFunctionNames() => Functions.Keys;

        private static UniTask<int> Abs(params int[] args)
        {
            if (args.Length != 1)
                throw new RiveRuntimeException("abs: expected 1 argument");
            
            return UniTask.FromResult(Math.Abs(args[0]));
        }

        private static UniTask<int> Min(params int[] args)
        {
            if (args.Length < 2)
                throw new RiveRuntimeException("min: expected at least 2 arguments");
            
            int result = args[0];
            for (int i = 1; i < args.Length; i++)
                if (args[i] < result) result = args[i];
            
            return UniTask.FromResult(result);
        }

        private static UniTask<int> Max(params int[] args)
        {
            if (args.Length < 2)
                throw new RiveRuntimeException("max: expected at least 2 arguments");
            
            int result = args[0];
            for (int i = 1; i < args.Length; i++)
                if (args[i] > result) result = args[i];
            
            return UniTask.FromResult(result);
        }

        private static UniTask<int> Clamp(params int[] args)
        {
            if (args.Length != 3)
                throw new RiveRuntimeException("clamp: expected 3 arguments (value, min, max)");
            
            int value = args[0];
            int min = args[1];
            int max = args[2];
            
            if (value < min) return UniTask.FromResult(min);
            if (value > max) return UniTask.FromResult(max);
            return UniTask.FromResult(value);
        }

        private static UniTask<int> Pow(params int[] args)
        {
            if (args.Length != 2)
                throw new RiveRuntimeException("pow: expected 2 arguments (base, exponent)");
            
            int baseVal = args[0];
            int exp = args[1];
            
            if (exp < 0)
                throw new RiveRuntimeException("pow: negative exponents not supported");
            
            int result = 1;
            for (int i = 0; i < exp; i++)
                result *= baseVal;
            
            return UniTask.FromResult(result);
        }

        private static UniTask<int> Sqrt(params int[] args)
        {
            if (args.Length != 1)
                throw new RiveRuntimeException("sqrt: expected 1 argument");
            
            if (args[0] < 0)
                throw new RiveRuntimeException("sqrt: negative values not supported");
            
            return UniTask.FromResult((int)Math.Sqrt(args[0]));
        }

        private static UniTask<int> Rand(params int[] args)
        {
            if (args.Length == 0)
                return UniTask.FromResult(UnityEngine.Random.Range(0, int.MaxValue));
            
            if (args.Length == 1)
                return UniTask.FromResult(UnityEngine.Random.Range(0, args[0]));
            
            if (args.Length == 2)
                return UniTask.FromResult(UnityEngine.Random.Range(args[0], args[1]));
            
            throw new RiveRuntimeException("rand: expected 0, 1 or 2 arguments");
        }

        private static UniTask<int> Sum(params int[] args)
        {
            if (args.Length == 0)
                throw new RiveRuntimeException("sum: expected at least 1 argument");
            
            int result = 0;
            for (int i = 0; i < args.Length; i++)
                result += args[i];
            
            return UniTask.FromResult(result);
        }

        private static UniTask<int> Avg(params int[] args)
        {
            if (args.Length == 0)
                throw new RiveRuntimeException("avg: expected at least 1 argument");
            
            int sum = 0;
            for (int i = 0; i < args.Length; i++)
                sum += args[i];
            
            return UniTask.FromResult(sum / args.Length);
        }
    }
}