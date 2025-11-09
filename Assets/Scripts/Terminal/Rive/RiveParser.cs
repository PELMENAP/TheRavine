using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using ZLinq;

namespace TheRavine.Base
{
    public class RiveParser
    {
        private static readonly Regex VarDeclarationRegex = new(@"int\s+(\w+)\s*=\s*(.+)", RegexOptions.Compiled);
        private static readonly Regex FunctionCallRegex = new(@"(\w+)\(([^)]*)\)", RegexOptions.Compiled);
        private static readonly Regex ForLoopRegex = new(@"for\s+(\w+)\s*=\s*(\d+)\s+to\s+(\d+)", RegexOptions.Compiled);
        private static readonly Regex ConditionRegex = new(@"(.+?)\s*(>|<|>=|<=|==|!=)\s*(.+)", RegexOptions.Compiled);
        private static readonly Regex VariableInCommandRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);
        
        public ProgramNode Parse(string fileName, string content)
        {
            var program = new ProgramNode { Name = fileName };
            
            var lines = PreprocessLines(content);
            
            if (lines.Count > 0 && lines[0].StartsWith("(") && lines[0].EndsWith(")"))
            {
                program.Parameters = ParseParameters(lines[0]);
                lines.RemoveAt(0);
            }
            program.Statements = ParseStatements(lines, 0, lines.Count, out _);
            return program;
        }
        
        private List<string> PreprocessLines(string content)
        {
            return content.Split('\n').AsValueEnumerable()
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("&"))
                .ToList();
        }
        
        private List<string> ParseParameters(string line)
        {
            var paramStr = line.Substring(1, line.Length - 2);
            if (string.IsNullOrEmpty(paramStr))
                return new List<string>();
                
            return paramStr.Split(',').AsValueEnumerable()
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();
        }
        
        private List<StatementNode> ParseStatements(List<string> lines, int start, int end, out int lastIndex)
        {
            var statements = new List<StatementNode>();
            lastIndex = start;
            
            for (int i = start; i < end && i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line == "end" || line == "else")
                {
                    lastIndex = i;
                    continue;
                }
                
                var statement = ParseStatement(line, lines, i, end, out int nextIndex);
                if (statement != null)
                {
                    statement.Line = i;
                    statements.Add(statement);
                    i = nextIndex - 1; // -1 because for loop will increment
                    lastIndex = nextIndex;
                }
            }
            
            return statements;
        }
        
        private StatementNode ParseStatement(string line, List<string> allLines, int currentIndex, int blockEnd, out int nextIndex)
        {
            nextIndex = currentIndex + 1;
            
            if (line.StartsWith(">>"))
                return ParseReturn(line);
                
            if (line.StartsWith("int "))
                return ParseVariableDeclaration(line);
                
            if (line.StartsWith("if "))
                return ParseIf(line, allLines, currentIndex, blockEnd, out nextIndex);
                
            if (line.StartsWith("for "))
                return ParseFor(line, allLines, currentIndex, blockEnd, out nextIndex);
                
            if (line.StartsWith("-"))
                return ParseTerminalCommand(line);
                
            if (line.StartsWith("log "))
                return ParseLog(line);
                
            if (line.Contains('=') && !line.StartsWith("int "))
                return ParseAssignment(line);
            
            throw new RiveParseException($"Unknown statement: {line}", currentIndex);
        }
        
        private ReturnNode ParseReturn(string line)
        {
            var expression = line.Substring(2).Trim();
            return new ReturnNode { Value = ParseExpression(expression) };
        }
        
        private VariableDeclarationNode ParseVariableDeclaration(string line)
        {
            var match = VarDeclarationRegex.Match(line);
            if (!match.Success)
                throw new RiveParseException($"Invalid variable declaration: {line}", 0);
                
            return new VariableDeclarationNode
            {
                Name = match.Groups[1].Value,
                InitialValue = ParseExpression(match.Groups[2].Value)
            };
        }
        
        private AssignmentNode ParseAssignment(string line)
        {
            var parts = line.Split('=');
            if (parts.Length != 2)
                throw new RiveParseException($"Invalid assignment: {line}", 0);
                
            return new AssignmentNode
            {
                VariableName = parts[0].Trim(),
                Value = ParseExpression(parts[1].Trim())
            };
        }
        
        private IfNode ParseIf(string line, List<string> allLines, int currentIndex, int blockEnd, out int nextIndex)
        {
            var condition = line.Substring(3).Trim();
            var (elseIndex, endIndex) = FindIfBlockBounds(allLines, currentIndex, blockEnd);
            
            if (endIndex == -1)
                throw new RiveParseException("Missing 'end' for if statement", currentIndex);
            
            var ifNode = new IfNode
            {
                Condition = ParseCondition(condition)
            };
            
            var thenEnd = elseIndex != -1 ? elseIndex : endIndex;
            ifNode.ThenBlock = ParseStatements(allLines, currentIndex + 1, thenEnd, out _);
            
            if (elseIndex != -1)
                ifNode.ElseBlock = ParseStatements(allLines, elseIndex + 1, endIndex, out _);
            
            nextIndex = endIndex + 1;
            return ifNode;
        }
        
        private ForLoopNode ParseFor(string line, List<string> allLines, int currentIndex, int blockEnd, out int nextIndex)
        {
            var match = ForLoopRegex.Match(line);
            if (!match.Success)
                throw new RiveParseException($"Invalid for loop syntax: {line}", currentIndex);
            
            var endIndex = FindEndStatement(allLines, currentIndex, blockEnd);
            if (endIndex == -1)
                throw new RiveParseException("Missing 'end' for for loop", currentIndex);
            
            var forNode = new ForLoopNode
            {
                VariableName = match.Groups[1].Value,
                StartValue = int.Parse(match.Groups[2].Value),
                EndValue = int.Parse(match.Groups[3].Value)
            };
            
            forNode.Body = ParseStatements(allLines, currentIndex + 1, endIndex, out _);
            
            nextIndex = endIndex + 1;
            return forNode;
        }
        
        private TerminalCommandNode ParseTerminalCommand(string line)
        {
            var variables = new List<string>();
            var matches = VariableInCommandRegex.Matches(line);
            foreach (Match match in matches)
            {
                variables.Add(match.Groups[1].Value);
            }
            
            return new TerminalCommandNode
            {
                Command = line,
                VariableReferences = variables
            };
        }
        
        private LogNode ParseLog(string line)
        {
            var expression = line.Substring(4).Trim();
            return new LogNode
            {
                Expression = ParseExpression(expression),
                OriginalExpression = expression
            };
        }
        
        private ExpressionNode ParseExpression(string expression)
        {
            expression = expression.Trim();
            
            // Function call
            var funcMatch = FunctionCallRegex.Match(expression);
            if (funcMatch.Success)
            {
                var funcCall = new FunctionCallNode
                {
                    FunctionName = funcMatch.Groups[1].Value
                };
                
                var argsStr = funcMatch.Groups[2].Value;
                if (!string.IsNullOrEmpty(argsStr))
                {
                    foreach (var arg in argsStr.Split(','))
                    {
                        funcCall.Arguments.Add(ParseExpression(arg.Trim()));
                    }
                }
                
                return funcCall;
            }
            
            return ParseArithmeticExpression(expression);
        }
        
        private ExpressionNode ParseCondition(string condition)
        {
            var match = ConditionRegex.Match(condition);
            if (!match.Success)
                throw new RiveParseException($"Invalid condition: {condition}", 0);
            
            var op = match.Groups[2].Value switch
            {
                ">" => BinaryOperator.Greater,
                "<" => BinaryOperator.Less,
                ">=" => BinaryOperator.GreaterOrEqual,
                "<=" => BinaryOperator.LessOrEqual,
                "==" => BinaryOperator.Equal,
                "!=" => BinaryOperator.NotEqual,
                _ => throw new RiveParseException($"Unknown operator: {match.Groups[2].Value}", 0)
            };
            
            return new BinaryOpNode
            {
                Left = ParseExpression(match.Groups[1].Value),
                Right = ParseExpression(match.Groups[3].Value),
                Operator = op
            };
        }
        
        private ExpressionNode ParseArithmeticExpression(string expression)
        {
            expression = expression.Replace(" ", "");
            
            // Простой парсер с учетом приоритета: +/- затем */
            // Для production лучше использовать Pratt parser или recursive descent
            
            // Ищем + или - (низкий приоритет)
            for (int i = expression.Length - 1; i >= 0; i--)
            {
                if (expression[i] == '+' || expression[i] == '-')
                {
                    return new BinaryOpNode
                    {
                        Left = ParseArithmeticExpression(expression.Substring(0, i)),
                        Right = ParseArithmeticExpression(expression.Substring(i + 1)),
                        Operator = expression[i] == '+' ? BinaryOperator.Add : BinaryOperator.Subtract
                    };
                }
            }
            
            // Ищем * или / (высокий приоритет)
            for (int i = expression.Length - 1; i >= 0; i--)
            {
                if (expression[i] == '*' || expression[i] == '/')
                {
                    return new BinaryOpNode
                    {
                        Left = ParseArithmeticExpression(expression.Substring(0, i)),
                        Right = ParseArithmeticExpression(expression.Substring(i + 1)),
                        Operator = expression[i] == '*' ? BinaryOperator.Multiply : BinaryOperator.Divide
                    };
                }
            }
            
            // Literal or variable
            if (int.TryParse(expression, out int value))
                return new LiteralNode { Value = value };
                
            return new VariableNode { Name = expression };
        }
        
        private (int elseIndex, int endIndex) FindIfBlockBounds(List<string> lines, int startIndex, int blockEnd)
        {
            int depth = 1;
            int elseIndex = -1;
            
            for (int i = startIndex + 1; i < blockEnd && i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("if ") || line.StartsWith("for "))
                    depth++;
                else if (line == "else" && depth == 1 && elseIndex == -1)
                    elseIndex = i;
                else if (line == "end")
                {
                    depth--;
                    if (depth == 0)
                        return (elseIndex, i);
                }
            }
            return (-1, -1);
        }
        
        private int FindEndStatement(List<string> lines, int startIndex, int blockEnd)
        {
            int depth = 1;
            for (int i = startIndex + 1; i < blockEnd && i < lines.Count; i++)
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
    }
    
    public class RiveParseException : Exception
    {
        public int Line { get; }
        
        public RiveParseException(string message, int line) : base($"Line {line}: {message}")
        {
            Line = line;
        }
    }
}