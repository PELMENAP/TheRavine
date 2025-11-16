using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class RiveExecutor
    {
        private readonly Stack<Dictionary<string, int>> _variableScopes = new();
        private readonly RiveRuntime _runtime;
        private int _operationCount;
        private const int MAX_OPERATIONS = 10000;
        
        public RiveExecutor(RiveRuntime runtime)
        {
            _runtime = runtime;
        }
        
        public async UniTask<ExecutionResult> ExecuteAsync(ProgramNode program, params int[] args)
        {
            if (args.Length != program.Parameters.Count)
            {
                return ExecutionResult.CreateError(
                    $"Argument count mismatch. Expected: {program.Parameters.Count}, got: {args.Length}");
            }
            
            _operationCount = 0;
            PushScope();
            
            try
            {
                for (int i = 0; i < program.Parameters.Count; i++)
                {
                    SetVariable(program.Parameters[i], args[i]);
                }
                
                return await ExecuteStatementsAsync(program.Statements);
            }
            catch (Exception ex)
            {
                return ExecutionResult.CreateError($"Execution error: {ex.Message}");
            }
            finally
            {
                PopScope();
            }
        }
        
        private async UniTask<ExecutionResult> ExecuteStatementsAsync(List<StatementNode> statements)
        {
            foreach (var statement in statements)
            {
                if (++_operationCount >= MAX_OPERATIONS)
                    return ExecutionResult.CreateError($"Operation limit exceeded ({MAX_OPERATIONS})");
                
                var result = await ExecuteStatementAsync(statement);
                
                if (result.Action != ExecutionAction.Continue)
                    return result;
                
                if (_operationCount % 10 == 0)
                    await UniTask.Yield();
            }
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteStatementAsync(StatementNode statement)
        {
            return statement switch
            {
                ReturnNode ret => await ExecuteReturnAsync(ret),
                VariableDeclarationNode varDecl => ExecuteVariableDeclaration(varDecl),
                AssignmentNode assign => await ExecuteAssignmentAsync(assign),
                IfNode ifNode => await ExecuteIfAsync(ifNode),
                ForLoopNode forLoop => await ExecuteForLoopAsync(forLoop),
                TerminalCommandNode termCmd => await ExecuteTerminalCommandAsync(termCmd),
                LogNode log => await ExecuteLogAsync(log),
                WaitNode wait => await ExecuteWaitAsync(wait),
                _ => ExecutionResult.CreateError($"Unknown statement type: {statement.GetType().Name}")
            };
        }
        
        private async UniTask<ExecutionResult> ExecuteReturnAsync(ReturnNode node)
        {
            var value = await EvaluateExpressionAsync(node.Value);
            return ExecutionResult.CreateReturn(value);
        }
        
        private ExecutionResult ExecuteVariableDeclaration(VariableDeclarationNode node)
        {
            var value = EvaluateExpression(node.InitialValue);
            SetVariable(node.Name, value);
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteAssignmentAsync(AssignmentNode node)
        {
            if (GetVariable(node.VariableName) == null)
                return ExecutionResult.CreateError($"Variable '{node.VariableName}' not declared");
            
            var value = await EvaluateExpressionAsync(node.Value);
            SetVariable(node.VariableName, value);
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteIfAsync(IfNode node)
        {
            var condition = await EvaluateConditionAsync(node.Condition);
            
            if (condition)
                return await ExecuteStatementsAsync(node.ThenBlock);
            else if (node.ElseBlock.Count > 0)
                return await ExecuteStatementsAsync(node.ElseBlock);
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteForLoopAsync(ForLoopNode node)
        {
            PushScope();
            
            try
            {
                var startValue = await EvaluateExpressionAsync(node.StartExpression);
                var endValue = await EvaluateExpressionAsync(node.EndExpression);
                
                for (int i = startValue; i <= endValue; i++)
                {
                    if (_operationCount >= MAX_OPERATIONS)
                        return ExecutionResult.CreateError("Operation limit exceeded in loop");
                    
                    SetVariable(node.VariableName, i);
                    var result = await ExecuteStatementsAsync(node.Body);
                    
                    if (result.Action == ExecutionAction.Return || result.Action == ExecutionAction.Stop)
                        return result;
                }
            }
            finally
            {
                PopScope();
            }
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteTerminalCommandAsync(TerminalCommandNode node)
        {
            var command = node.Command;
            
            foreach (var varName in node.VariableReferences)
            {
                var value = GetVariable(varName);
                if (value.HasValue)
                    command = command.Replace($"{varName}", value.Value.ToString());
            }
            
            var success = await _runtime.ExecuteTerminalCommandAsync(command);
            return success ? ExecutionResult.CreateSuccess() : 
                ExecutionResult.CreateError($"Command failed: {command}");
        }

        private async UniTask<ExecutionResult> ExecuteLogAsync(LogNode node)
        {
            var value = await EvaluateExpressionAsync(node.Expression);
            await _runtime.ExecuteTerminalCommandAsync($"~print {node.OriginalExpression} = {value}");
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteWaitAsync(WaitNode node)
        {
            var milliseconds = await EvaluateExpressionAsync(node.Milliseconds);
            
            if (milliseconds < 0)
                return ExecutionResult.CreateError("Wait time cannot be negative");
            
            if (milliseconds > 60000)
                return ExecutionResult.CreateError("Wait time cannot exceed 60000ms (60 seconds)");
            
            await UniTask.Delay(milliseconds);
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<int> EvaluateExpressionAsync(ExpressionNode expr)
        {
            if (expr is FunctionCallNode funcCall)
            {
                var args = new List<int>();
                foreach (var arg in funcCall.Arguments)
                {
                    args.Add(await EvaluateExpressionAsync(arg));
                }
                
                var result = await _runtime.CallFunctionAsync(funcCall.FunctionName, args.ToArray());
                return result.Success ? result.ReturnValue : 0;
            }
            
            return EvaluateExpression(expr);
        }
        
        private int EvaluateExpression(ExpressionNode expr)
        {
            return expr switch
            {
                LiteralNode lit => lit.Value,
                VariableNode var => GetVariable(var.Name) ?? 0,
                BinaryOpNode bin => EvaluateBinaryOp(bin),
                _ => 0
            };
        }
        
        private int EvaluateBinaryOp(BinaryOpNode node)
        {
            var left = EvaluateExpression(node.Left);
            var right = EvaluateExpression(node.Right);
            
            return node.Operator switch
            {
                BinaryOperator.Add => left + right,
                BinaryOperator.Subtract => left - right,
                BinaryOperator.Multiply => left * right,
                BinaryOperator.Divide => right != 0 ? left / right : 0,
                _ => 0
            };
        }
        
        private async UniTask<bool> EvaluateConditionAsync(ExpressionNode expr)
        {
            if (expr is not BinaryOpNode bin)
                return false;
            
            var left = await EvaluateExpressionAsync(bin.Left);
            var right = await EvaluateExpressionAsync(bin.Right);
            
            return bin.Operator switch
            {
                BinaryOperator.Greater => left > right,
                BinaryOperator.Less => left < right,
                BinaryOperator.GreaterOrEqual => left >= right,
                BinaryOperator.LessOrEqual => left <= right,
                BinaryOperator.Equal => left == right,
                BinaryOperator.NotEqual => left != right,
                _ => false
            };
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
                _variableScopes.Peek()[name] = value;
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
    }
    
    public struct ExecutionResult
    {
        public bool Success;
        public int ReturnValue;
        public string ErrorMessage;
        public ExecutionAction Action;
        
        public static ExecutionResult CreateSuccess(int returnValue = 0) =>
            new() { Success = true, ReturnValue = returnValue, Action = ExecutionAction.Continue };
        
        public static ExecutionResult CreateReturn(int value) =>
            new() { Success = true, ReturnValue = value, Action = ExecutionAction.Return };
        
        public static ExecutionResult CreateError(string message) =>
            new() { Success = false, ErrorMessage = message, Action = ExecutionAction.Stop };
    }
    
    public enum ExecutionAction
    {
        Continue,
        Return,
        Stop
    }
}