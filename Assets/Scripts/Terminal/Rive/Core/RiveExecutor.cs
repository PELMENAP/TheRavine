using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class RiveExecutor
    {
        private readonly RiveRuntime _runtime;
        private readonly InputStreamManager _inputStream;
        private readonly InteractorRegistry _interactorRegistry;
        private const int MAX_OPERATIONS = 10000;
        public const int MaxCallDepth = 32;
        public const int MaxStackDepth = 128;
        public const int MaxInputWaitMs = 60000;
        public const int MaxTotalWaitMs = 300000;
        
        public RiveExecutor(RiveRuntime runtime, InputStreamManager inputStream, InteractorRegistry interactorRegistry)
        {
            _runtime = runtime;
            _inputStream = inputStream;
            _interactorRegistry = interactorRegistry;
        }
        
        public async UniTask<ExecutionResult> ExecuteAsync(ProgramNode program, int[] args, RiveRun run)
        {
            if (run.CallDepth >= MaxCallDepth)
            {
                run.AbortMessage ??= $"Call depth limit exceeded ({MaxCallDepth})";
                return ExecutionResult.CreateError(run.AbortMessage);
            }

            if (args.Length != program.Parameters.Count)
            {
                return ExecutionResult.CreateError(
                    $"Argument count mismatch. Expected: {program.Parameters.Count}, got: {args.Length}");
            }

            var frame = new RiveFrame(run);
            frame.PushScope();
            run.CallDepth++;

            try
            {
                for (int i = 0; i < program.Parameters.Count; i++)
                {
                    frame.SetVariable(program.Parameters[i], args[i]);
                }

                var result = await ExecuteStatementsAsync(program.Statements, frame);

                if (run.AbortMessage != null)
                    return ExecutionResult.CreateError(run.AbortMessage);

                return result;
            }
            catch (OperationCanceledException)
            {
                run.AbortMessage ??= "Script cancelled";
                return ExecutionResult.CreateError(run.AbortMessage);
            }
            catch (Exception ex)
            {
                return ExecutionResult.CreateError($"Execution error: {ex.Message}");
            }
            finally
            {
                run.CallDepth--;
                frame.PopScope();
            }
        }

        private async UniTask<ExecutionResult> ExecuteStatementsAsync(List<StatementNode> statements, RiveFrame frame)
        {
            var run = frame.Run;

            if (++run.StackDepth > MaxStackDepth)
            {
                run.StackDepth--;
                run.AbortMessage ??= $"Evaluation depth limit exceeded ({MaxStackDepth})";
                return ExecutionResult.CreateError(run.AbortMessage);
            }

            try
            {
                foreach (var statement in statements)
                {
                    if (run.AbortMessage != null)
                        return ExecutionResult.CreateError(run.AbortMessage);

                    if (++run.OperationCount >= MAX_OPERATIONS)
                    {
                        run.AbortMessage = $"Operation limit exceeded ({MAX_OPERATIONS})";
                        return ExecutionResult.CreateError(run.AbortMessage);
                    }

                    var result = await ExecuteStatementAsync(statement, frame);

                    if (result.Action != ExecutionAction.Continue)
                        return result;

                    if (run.OperationCount % 10 == 0)
                        await UniTask.Yield(run.Token);
                }

                return ExecutionResult.CreateSuccess();
            }
            finally
            {
                run.StackDepth--;
            }
        }

        private async UniTask<ExecutionResult> ExecuteStatementAsync(StatementNode statement, RiveFrame frame)
        {
            return statement switch
            {
                ReturnNode ret => await ExecuteReturnAsync(ret, frame),
                VariableDeclarationNode varDecl => await ExecuteVariableDeclarationAsync(varDecl, frame),
                AssignmentNode assign => await ExecuteAssignmentAsync(assign, frame),
                IfNode ifNode => await ExecuteIfAsync(ifNode, frame),
                ForLoopNode forLoop => await ExecuteForLoopAsync(forLoop, frame),
                TerminalCommandNode termCmd => await ExecuteTerminalCommandAsync(termCmd, frame),
                LogNode log => await ExecuteLogAsync(log, frame),
                WaitNode wait => await ExecuteWaitAsync(wait, frame),
                ExpressionStatementNode exprStmt => await ExecuteExpressionStatementAsync(exprStmt, frame),
                IndexAssignNode idxAssign        => await ExecuteIndexAssignAsync(idxAssign, frame),
                _ => ExecutionResult.CreateError($"Unknown statement type: {statement.GetType().Name}")
            };
        }
        
        private async UniTask<ExecutionResult> ExecuteReturnAsync(ReturnNode node, RiveFrame frame)
        {
            var value = await EvaluateExpressionAsync(node.Value, frame);
            return ExecutionResult.CreateReturn(value);
        }
        
        private async UniTask<ExecutionResult> ExecuteVariableDeclarationAsync(VariableDeclarationNode node, RiveFrame frame)
        {
            var value = await EvaluateExpressionAsync(node.InitialValue, frame);
            frame.SetVariable(node.Name, value);
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteAssignmentAsync(AssignmentNode node, RiveFrame frame)
        {
            if (frame.GetVariable(node.VariableName) == null)
                return ExecutionResult.CreateError($"Variable '{node.VariableName}' not declared");
            
            var value = await EvaluateExpressionAsync(node.Value, frame);
            frame.SetVariable(node.VariableName, value);
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteIfAsync(IfNode node, RiveFrame frame)
        {
            var condition = await EvaluateConditionAsync(node.Condition, frame);
            
            if (condition)
                return await ExecuteStatementsAsync(node.ThenBlock, frame);
            else if (node.ElseBlock.Count > 0)
                return await ExecuteStatementsAsync(node.ElseBlock, frame);
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteForLoopAsync(ForLoopNode node, RiveFrame frame)
        {
            frame.PushScope();
            
            try
            {
                var startValue = await EvaluateExpressionAsync(node.StartExpression, frame);
                var endValue = await EvaluateExpressionAsync(node.EndExpression, frame);

                if (startValue is not int s || endValue is not int e)
                    return ExecutionResult.CreateError("You can loop only int values");
                
                var run = frame.Run;
                
                for (long i = s; i <= e; i++)
                {
                    if (run.AbortMessage != null)
                        return ExecutionResult.CreateError(run.AbortMessage);
                    
                    if (++run.OperationCount >= MAX_OPERATIONS)
                    {
                        run.AbortMessage = $"Operation limit exceeded ({MAX_OPERATIONS})";
                        return ExecutionResult.CreateError(run.AbortMessage);
                    }
                    
                    frame.SetVariable(node.VariableName, (int)i);
                    var result = await ExecuteStatementsAsync(node.Body, frame);
                    
                    if (result.Action == ExecutionAction.Return || result.Action == ExecutionAction.Stop)
                        return result;
                    
                    if (node.Body.Count == 0 && run.OperationCount % 10 == 0)
                        await UniTask.Yield(run.Token);
                }
            }
            finally
            {
                frame.PopScope();
            }
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteTerminalCommandAsync(TerminalCommandNode node, RiveFrame frame)
        {
            var command = node.VariableReferences.Count == 0
                ? node.Command
                : SubstituteVariables(node.Command, frame);
            
            var success = await _runtime.ExecuteTerminalCommandAsync(command);
            return success ? ExecutionResult.CreateSuccess() : 
                ExecutionResult.CreateError($"Command failed: {command}");
        }

        private static string SubstituteVariables(string command, RiveFrame frame)
        {
            var sb = new StringBuilder(command.Length + 16);
            int i = 0;
            
            while (i < command.Length)
            {
                if (command[i] == '{')
                {
                    int end = i + 1;
                    while (end < command.Length && (char.IsLetterOrDigit(command[end]) || command[end] == '_'))
                        end++;
                    
                    if (end > i + 1 && end < command.Length && command[end] == '}')
                    {
                        var value = frame.GetVariable(command.Substring(i + 1, end - i - 1));
                        if (value != null)
                        {
                            sb.Append(value.ToRiveString());
                            i = end + 1;
                            continue;
                        }
                    }
                }
                
                sb.Append(command[i]);
                i++;
            }
            
            return sb.ToString();
        }

        private async UniTask<ExecutionResult> ExecuteLogAsync(LogNode node, RiveFrame frame)
        {
            var value = await EvaluateExpressionAsync(node.Expression, frame);
            await _runtime.ExecuteTerminalCommandAsync($"~print {node.OriginalExpression} = {value}");
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<ExecutionResult> ExecuteWaitAsync(WaitNode node, RiveFrame frame)
        {
            var milliseconds = await EvaluateExpressionAsync(node.Milliseconds, frame);

            if(milliseconds is not int mil)
                return ExecutionResult.CreateError("Wait time should be a int number");
            
            if (mil < 0)
                return ExecutionResult.CreateError("Wait time cannot be negative");
            
            if (mil > 60000)
                return ExecutionResult.CreateError("Wait time cannot exceed more than 60000ms (60 seconds)");
            
            var run = frame.Run;
            if (run.WaitedMilliseconds + mil > MaxTotalWaitMs)
            {
                run.AbortMessage = $"Total wait budget exceeded ({MaxTotalWaitMs}ms)";
                return ExecutionResult.CreateError(run.AbortMessage);
            }
            
            run.WaitedMilliseconds += mil;
            await UniTask.Delay(mil, cancellationToken: run.Token, cancelImmediately: true);
            
            return ExecutionResult.CreateSuccess();
        }
        
        private async UniTask<object> EvaluateExpressionAsync(ExpressionNode expr, RiveFrame frame)
        {
            var run = frame.Run;

            if (++run.StackDepth > MaxStackDepth)
            {
                run.StackDepth--;
                run.AbortMessage ??= $"Evaluation depth limit exceeded ({MaxStackDepth})";
                return 0;
            }

            try
            {
                return expr switch
                {
                    LiteralNode lit => lit.Value,
                    VariableNode var => frame.GetVariable(var.Name) ?? 0,
                    BinaryOpNode bin => await EvaluateBinaryOpAsync(bin, frame),
                    FunctionCallNode funcCall => await EvaluateFunctionCallAsync(funcCall, frame),
                    GetInputNode => await EvaluateGetAsync(frame),
                    SendToInteractorNode send => await EvaluateSendAsync(send, frame),
                    UnaryOpNode unary       => await EvaluateUnaryOpAsync(unary, frame),
                    ArrayLiteralNode arrLit => await EvaluateArrayLiteralAsync(arrLit, frame),
                    IndexAccessNode idxAcc  => await EvaluateIndexAccessAsync(idxAcc, frame),
                    _ => 0
                };
            }
            finally
            {
                run.StackDepth--;
            }
        }
        
        private async UniTask<object> EvaluateGetAsync(RiveFrame frame)
        {
            var run = frame.Run;
            var remaining = MaxTotalWaitMs - run.WaitedMilliseconds;
            
            if (remaining <= 0)
            {
                run.AbortMessage ??= $"Total wait budget exceeded ({MaxTotalWaitMs}ms)";
                return 0;
            }
            
            var timeout = Math.Min(MaxInputWaitMs, remaining);
            var started = Stopwatch.GetTimestamp();
            
            try
            {
                return await _inputStream.GetAsync(run.Token, timeout);
            }
            catch (TimeoutException)
            {
                run.AbortMessage ??= timeout < MaxInputWaitMs
                    ? $"Total wait budget exceeded ({MaxTotalWaitMs}ms)"
                    : $"Input timed out ({MaxInputWaitMs}ms)";
                return 0;
            }
            finally
            {
                var elapsed = (Stopwatch.GetTimestamp() - started) * 1000 / Stopwatch.Frequency;
                run.WaitedMilliseconds += (int)Math.Min(elapsed, MaxTotalWaitMs);
            }
        }

        private async UniTask<object> EvaluateBinaryOpAsync(BinaryOpNode node, RiveFrame frame)
        {
            var left = await EvaluateExpressionAsync(node.Left, frame);
            var right = await EvaluateExpressionAsync(node.Right, frame);
            
            if (left is not int l || right is not int r)
                return 0;
            
            return node.Operator switch
            {
                BinaryOperator.Add => l + r,
                BinaryOperator.Subtract => l - r,
                BinaryOperator.Multiply => l * r,
                BinaryOperator.Divide => r != 0 ? l / r : 0,
                BinaryOperator.And => (l != 0 && r != 0) ? 1 : 0,
                BinaryOperator.Or  => (l != 0 || r != 0) ? 1 : 0,

                BinaryOperator.Greater => l > r ? 1 : 0,
                BinaryOperator.Less => l < r ? 1 : 0,
                BinaryOperator.GreaterOrEqual => l >= r ? 1 : 0,
                BinaryOperator.LessOrEqual => l <= r ? 1 : 0,
                BinaryOperator.Equal => l == r ? 1 : 0,
                BinaryOperator.NotEqual => l != r ? 1 : 0,
                _ => 0
            };
        }

        private async UniTask<object> EvaluateFunctionCallAsync(FunctionCallNode funcCall, RiveFrame frame)
        {
            var args = new object[funcCall.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = await EvaluateExpressionAsync(funcCall.Arguments[i], frame);
            }
            
            var result = await _runtime.CallFunctionAsync(funcCall.FunctionName, frame.Run, args);
            return result.Success ? result.ReturnValue : 0;
        }

        private async UniTask<int> EvaluateSendAsync(SendToInteractorNode node, RiveFrame frame)
        {
            var value = await EvaluateExpressionAsync(node.Value, frame);
            if (value is not int intValue)
                return 0;
            return await _interactorRegistry.SendToInteractorAsync(node.InteractorName, intValue);
        }

        private async UniTask<object> EvaluateUnaryOpAsync(UnaryOpNode node, RiveFrame frame)
        {
            var operand = await EvaluateExpressionAsync(node.Operand, frame);
            if (operand is not int v) return 0;
            return node.Operator switch
            {
                UnaryOperator.Negate => -v,
                UnaryOperator.Not    => v == 0 ? 1 : 0,
                _ => 0
            };
        }

        private async UniTask<object> EvaluateArrayLiteralAsync(ArrayLiteralNode node, RiveFrame frame)
        {
            var list = new List<int>(node.Elements.Count);
            foreach (var el in node.Elements)
            {
                var val = await EvaluateExpressionAsync(el, frame);
                list.Add(val is int i ? i : 0);
            }
            return list;
        }

        private async UniTask<object> EvaluateIndexAccessAsync(IndexAccessNode node, RiveFrame frame)
        {
            var arr = frame.GetVariable(node.ArrayName);
            if (arr is not List<int> list) return 0;
            
            var idx = await EvaluateExpressionAsync(node.Index, frame);
            if (idx is not int i || i < 0 || i >= list.Count) return 0;
            
            return list[i];
        }
                
        private async UniTask<bool> EvaluateConditionAsync(ExpressionNode expr, RiveFrame frame)
        {
            var result = await EvaluateExpressionAsync(expr, frame);
            if (result is int iv) return iv != 0;
            return false;
        }
        private async UniTask<ExecutionResult> ExecuteExpressionStatementAsync(ExpressionStatementNode node, RiveFrame frame)
        {
            await EvaluateExpressionAsync(node.Expression, frame);
            return ExecutionResult.CreateSuccess();
        }

        private async UniTask<ExecutionResult> ExecuteIndexAssignAsync(IndexAssignNode node, RiveFrame frame)
        {
            var arr = frame.GetVariable(node.ArrayName);
            if (arr is not List<int> list)
                return ExecutionResult.CreateError($"'{node.ArrayName}' is not an array");
            
            var idx = await EvaluateExpressionAsync(node.Index, frame);
            var val = await EvaluateExpressionAsync(node.Value, frame);
            
            if (idx is not int i || val is not int v)
                return ExecutionResult.CreateError("Array index and value must be int");
            
            if (i < 0 || i >= list.Count)
                return ExecutionResult.CreateError($"Index {i} out of range [0..{list.Count - 1}]");
            
            list[i] = v;
            return ExecutionResult.CreateSuccess();
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