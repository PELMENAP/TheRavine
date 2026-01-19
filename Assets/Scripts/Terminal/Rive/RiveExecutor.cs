using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class RiveExecutor
    {
        private readonly Stack<Dictionary<string, object>> _variableScopes = new();
        private readonly RiveRuntime _runtime;
        private readonly InputStreamManager _inputStream;
        private readonly InteractorRegistry _interactorRegistry;
        private int _operationCount;
        private const int MAX_OPERATIONS = 10000;
        
        public RiveExecutor(RiveRuntime runtime, InputStreamManager inputStream, InteractorRegistry interactorRegistry)
        {
            _runtime = runtime;
            _inputStream = inputStream;
            _interactorRegistry = interactorRegistry;
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
                VariableDeclarationNode varDecl => await ExecuteVariableDeclarationAsync(varDecl),
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
        
        private async UniTask<ExecutionResult> ExecuteVariableDeclarationAsync(VariableDeclarationNode node)
        {
            var value = await EvaluateExpressionAsync(node.InitialValue);
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

                if (startValue is not int s || endValue is not int e)
                    return ExecutionResult.CreateError("You can loop only int values");
                
                for (int i = s; i <= e; i++)
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

                if (value != null)
                    command = command.Replace($"{varName}", value.ToRiveString());
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

            if(milliseconds is not int mil)
                return ExecutionResult.CreateError("Wait time should be a int number");
            
            if (mil < 0)
                return ExecutionResult.CreateError("Wait time cannot be negative");
            
            if (mil > 60000)
                return ExecutionResult.CreateError("Wait time cannot exceed more than 60000ms (60 seconds)");
            
            await UniTask.Delay(mil);
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<object> EvaluateExpressionAsync(ExpressionNode expr)
        {
            return expr switch
            {
                LiteralNode lit => lit.Value,
                QbitLiteralNode qlit => new Qbit(qlit.InitialState),
                VariableNode var => GetVariable(var.Name) ?? 0,
                BinaryOpNode bin => await EvaluateBinaryOpAsync(bin),
                FunctionCallNode funcCall => await EvaluateFunctionCallAsync(funcCall),
                GetInputNode => await _inputStream.GetAsync(),
                SendToInteractorNode send => await EvaluateSendAsync(send),
                _ => 0
            };
        }
        
        private async UniTask<object> EvaluateBinaryOpAsync(BinaryOpNode node)
        {
            var left = await EvaluateExpressionAsync(node.Left);
            var right = await EvaluateExpressionAsync(node.Right);
            
            if (left is not int l || right is not int r)
                return 0;
            
            return node.Operator switch
            {
                BinaryOperator.Add => l + r,
                BinaryOperator.Subtract => l - r,
                BinaryOperator.Multiply => l * r,
                BinaryOperator.Divide => r != 0 ? l / r : 0,
                _ => 0
            };
        }

        private async UniTask<object> EvaluateFunctionCallAsync(FunctionCallNode funcCall)
        {
            var args = new List<object>();
            foreach (var arg in funcCall.Arguments)
            {
                args.Add(await EvaluateExpressionAsync(arg));
            }
            
            var result = await _runtime.CallFunctionAsync(funcCall.FunctionName, args.ToArray());
            return result.Success ? result.ReturnValue : 0;
        }

        private async UniTask<int> EvaluateSendAsync(SendToInteractorNode node)
        {
            var value = await EvaluateExpressionAsync(node.Value);
            if (value is not int intValue)
                return 0;
            return await _interactorRegistry.SendToInteractorAsync(node.InteractorName, intValue);
        }
        
        private async UniTask<bool> EvaluateConditionAsync(ExpressionNode expr)
        {
            if (expr is not BinaryOpNode bin)
                return false;
            
            var left = await EvaluateExpressionAsync(bin.Left);
            var right = await EvaluateExpressionAsync(bin.Right);
            
            if (left is not int l || right is not int r)
                return false;
            
            return bin.Operator switch
            {
                BinaryOperator.Greater => l > r,
                BinaryOperator.Less => l < r,
                BinaryOperator.GreaterOrEqual => l >= r,
                BinaryOperator.LessOrEqual => l <= r,
                BinaryOperator.Equal => l == r,
                BinaryOperator.NotEqual => l != r,
                _ => false
            };
        }
        
        private void PushScope()
        {
            _variableScopes.Push(new Dictionary<string, object>());
        }
        
        private void PopScope()
        {
            if (_variableScopes.Count > 0)
                _variableScopes.Pop();
        }
        
        private void SetVariable(string name, object value)
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
        
        private object GetVariable(string name)
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
        public object ReturnValue;
        public string ErrorMessage;
        public ExecutionAction Action;
        
        public static ExecutionResult CreateSuccess(object returnValue = null) =>
            new() { Success = true, ReturnValue = returnValue ?? 0, Action = ExecutionAction.Continue };
        
        public static ExecutionResult CreateReturn(object value) =>
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