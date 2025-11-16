using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ZLinq;

namespace TheRavine.Base
{
    public class RiveParser
    {
        private readonly IRavineLogger _logger;
        private List<Token> _tokens;
        private int _position;

        public RiveParser(IRavineLogger logger)
        {
            _logger = logger;
        }
        
        private static readonly Dictionary<string, TokenType> Keywords = new()
        {
            ["int"] = TokenType.Int,
            ["if"] = TokenType.If,
            ["else"] = TokenType.Else,
            ["for"] = TokenType.For,
            ["to"] = TokenType.To,
            ["end"] = TokenType.End,
            ["log"] = TokenType.Log,
            ["wait"] = TokenType.Wait 
        };
        
        public ProgramNode Parse(string fileName, string content)
        {
            var lines = PreprocessLines(content);
            _tokens = Tokenize(lines);
            _position = 0;
            
            var program = new ProgramNode { Name = fileName };
            
            if (Match(TokenType.LeftParen))
            {
                program.Parameters = ParseParameters();
                Consume(TokenType.RightParen, "Expected ')' after parameters");
            }
            
            program.Statements = ParseStatements(TokenType.EOF);
            
            return program;
        }
        
        private List<string> PreprocessLines(string content)
        {
            return content.Split('\n').AsValueEnumerable()
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("&"))
                .ToList();
        }
        
        private List<Token> Tokenize(List<string> lines)
        {
            var tokens = new List<Token>();
            
            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                int pos = 0;
                
                while (pos < line.Length)
                {
                    if (char.IsWhiteSpace(line[pos]))
                    {
                        pos++;
                        continue;
                    }
                    
                    if (line.Substring(pos).StartsWith(">>"))
                    {
                        tokens.Add(new Token(TokenType.Return, ">>", lineIndex));
                        pos += 2;
                        continue;
                    }
                    
                    if (line[pos] == '~')
                    {
                        var cmdEnd = line.Length;
                        tokens.Add(new Token(TokenType.TerminalCommand, line.Substring(pos, cmdEnd - pos), lineIndex));
                        pos = cmdEnd;
                        continue;
                    }
                    
                    if (pos + 1 < line.Length)
                    {
                        var twoChar = line.Substring(pos, 2);
                        if (twoChar == ">=" || twoChar == "<=" || twoChar == "==" || twoChar == "!=")
                        {
                            tokens.Add(new Token(TokenType.Operator, twoChar, lineIndex));
                            pos += 2;
                            continue;
                        }
                    }
                    
                    if ("(){}=+-*/<>,".Contains(line[pos]))
                    {
                        var type = line[pos] switch
                        {
                            '(' => TokenType.LeftParen,
                            ')' => TokenType.RightParen,
                            '{' => TokenType.LeftBrace,
                            '}' => TokenType.RightBrace,
                            '=' => TokenType.Equal,
                            ',' => TokenType.Comma,
                            _ => TokenType.Operator
                        };
                        tokens.Add(new Token(type, line[pos].ToString(), lineIndex));
                        pos++;
                        continue;
                    }
                    
                    if (char.IsDigit(line[pos]))
                    {
                        int start = pos;
                        while (pos < line.Length && char.IsDigit(line[pos]))
                            pos++;
                        tokens.Add(new Token(TokenType.Number, line.Substring(start, pos - start), lineIndex));
                        continue;
                    }

                    if (char.IsLetter(line[pos]) || line[pos] == '_')
                    {
                        int start = pos;
                        while (pos < line.Length && (char.IsLetterOrDigit(line[pos]) || line[pos] == '_'))
                            pos++;

                        var word = line.Substring(start, pos - start);
                        var type = Keywords.TryGetValue(word, out var kwType) ? kwType : TokenType.Identifier;
                        tokens.Add(new Token(type, word, lineIndex));
                        continue;
                    }
                    
                    throw LogAndThrow($"Unexpected character: {line[pos]}");
                }
                
                tokens.Add(new Token(TokenType.NewLine, "\n", lineIndex));
            }
            
            tokens.Add(new Token(TokenType.EOF, "", lines.Count));
            return tokens;
        }
        
        private List<string> ParseParameters()
        {
            var parameters = new List<string>();
            
            if (Check(TokenType.RightParen))
                return parameters;
            
            do
            {
                var token = Consume(TokenType.Identifier, "Expected parameter name");
                parameters.Add(token.Value);
            } while (Match(TokenType.Comma));
            
            return parameters;
        }
        
        private List<StatementNode> ParseStatements(TokenType endToken)
        {
            var statements = new List<StatementNode>();
            SkipNewLines();
            
            while (!Check(endToken) && !Check(TokenType.Else))
            {
                statements.Add(ParseStatement());
                SkipNewLines();
            }
            
            return statements;
        }
        
        private StatementNode ParseStatement()
        {
            var lineNumber = Current().Line;
            StatementNode statement;

            if (Match(TokenType.Return))
                statement = new ReturnNode { Value = ParseExpression() };
            else if (Match(TokenType.Int))
                statement = ParseVariableDeclaration();
            else if (Match(TokenType.If))
                statement = ParseIf();
            else if (Match(TokenType.For))
                statement = ParseFor();
            else if (Match(TokenType.Log))
                statement = ParseLog();
            else if (Match(TokenType.Wait))
                statement = ParseWait();
            else if (Match(TokenType.TerminalCommand))
                statement = ParseTerminalCommand(Previous().Value);
            else if (Check(TokenType.Identifier) && Peek(1)?.Type == TokenType.Equal)
                statement = ParseAssignment();
            else
                throw LogAndThrow($"Unexpected token: {Current().Value}");
            
            statement.Line = lineNumber;
            return statement;
        }
        
        private VariableDeclarationNode ParseVariableDeclaration()
        {
            var name = Consume(TokenType.Identifier, "Expected variable name");
            Consume(TokenType.Equal, "Expected '=' in variable declaration");
            var value = ParseExpression();
            
            return new VariableDeclarationNode
            {
                Name = name.Value,
                InitialValue = value
            };
        }
        
        private AssignmentNode ParseAssignment()
        {
            var name = Consume(TokenType.Identifier, "Expected variable name");
            Consume(TokenType.Equal, "Expected '='");
            var value = ParseExpression();
            
            return new AssignmentNode
            {
                VariableName = name.Value,
                Value = value
            };
        }
        
        private IfNode ParseIf()
        {
            var condition = ParseExpression();
            SkipNewLines();
            
            var thenBlock = ParseStatements(TokenType.End);
            List<StatementNode> elseBlock = new();
            
            if (Match(TokenType.Else))
            {
                SkipNewLines();
                elseBlock = ParseStatements(TokenType.End);
            }
            
            Consume(TokenType.End, "Expected 'end' after if block");
            
            return new IfNode
            {
                Condition = condition,
                ThenBlock = thenBlock,
                ElseBlock = elseBlock
            };
        }
        
        private ForLoopNode ParseFor()
        {
            var varName = Consume(TokenType.Identifier, "Expected loop variable");
            Consume(TokenType.Equal, "Expected '=' in for loop");
            
            var startExpr = ParseExpression();
            Consume(TokenType.To, "Expected 'to' in for loop");
            
            var endExpr = ParseExpression();
            SkipNewLines();
            
            var body = ParseStatements(TokenType.End);
            Consume(TokenType.End, "Expected 'end' after for loop");
            
            return new ForLoopNode
            {
                VariableName = varName.Value,
                StartExpression = startExpr,
                EndExpression = endExpr,
                Body = body
            };
        }

        private LogNode ParseLog()
        {
            var exprStart = _position;
            var expr = ParseExpression();

            var originalTokens = _tokens.AsValueEnumerable()
                .Skip(exprStart)
                .Take(_position - exprStart)
                .Select(t => t.Value)
                .ToList();

            var originalExpr = string.Join("", originalTokens);

            return new LogNode
            {
                Expression = expr,
                OriginalExpression = originalExpr
            };
        }
        private WaitNode ParseWait()
        {
            var milliseconds = ParseExpression();
            
            return new WaitNode
            {
                Milliseconds = milliseconds
            };
        }
        
        private TerminalCommandNode ParseTerminalCommand(string command)
        {
            var variables = new List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(command, @"\{(\w+)\}");
            
            foreach (System.Text.RegularExpressions.Match match in matches)
                variables.Add(match.Groups[1].Value);
            
            return new TerminalCommandNode
            {
                Command = command,
                VariableReferences = variables
            };
        }
        
        private ExpressionNode ParseExpression()
        {
            return ParseComparison();
        }
        
        private ExpressionNode ParseComparison()
        {
            var expr = ParseAdditive();
            
            if (Check(TokenType.Operator))
            {
                var op = Current().Value;
                if (op == ">" || op == "<" || op == ">=" || op == "<=" || op == "==" || op == "!=")
                {
                    Advance();
                    var right = ParseAdditive();
                    
                    var binaryOp = op switch
                    {
                        ">" => BinaryOperator.Greater,
                        "<" => BinaryOperator.Less,
                        ">=" => BinaryOperator.GreaterOrEqual,
                        "<=" => BinaryOperator.LessOrEqual,
                        "==" => BinaryOperator.Equal,
                        "!=" => BinaryOperator.NotEqual,
                        _ => throw LogAndThrow($"Unknown operator: {op}")
                    };
                    
                    return new BinaryOpNode
                    {
                        Left = expr,
                        Right = right,
                        Operator = binaryOp
                    };
                }
            }
            
            return expr;
        }
        
        private ExpressionNode ParseAdditive()
        {
            var expr = ParseMultiplicative();
            
            while (Check(TokenType.Operator) && (Current().Value == "+" || Current().Value == "-"))
            {
                var op = Current().Value;
                Advance();
                var right = ParseMultiplicative();
                
                expr = new BinaryOpNode
                {
                    Left = expr,
                    Right = right,
                    Operator = op == "+" ? BinaryOperator.Add : BinaryOperator.Subtract
                };
            }
            
            return expr;
        }
        
        private ExpressionNode ParseMultiplicative()
        {
            var expr = ParsePrimary();
            
            while (Check(TokenType.Operator) && (Current().Value == "*" || Current().Value == "/"))
            {
                var op = Current().Value;
                Advance();
                var right = ParsePrimary();
                
                expr = new BinaryOpNode
                {
                    Left = expr,
                    Right = right,
                    Operator = op == "*" ? BinaryOperator.Multiply : BinaryOperator.Divide
                };
            }
            
            return expr;
        }

        private ExpressionNode ParsePrimary()
        {
            if (Match(TokenType.Number))
                return new LiteralNode { Value = int.Parse(Previous().Value) };

            if (Match(TokenType.LeftParen))
            {
                var expr = ParseExpression();
                Consume(TokenType.RightParen, "Expected ')' after expression");
                return expr;
            }

            if (Check(TokenType.Identifier))
            {
                var name = Advance().Value;

                if (Match(TokenType.LeftParen))
                {
                    var args = new List<ExpressionNode>();

                    if (!Check(TokenType.RightParen))
                    {
                        do
                        {
                            args.Add(ParseExpression());
                        } while (Match(TokenType.Comma));
                    }

                    Consume(TokenType.RightParen, "Expected ')' after function arguments");

                    return new FunctionCallNode
                    {
                        FunctionName = name,
                        Arguments = args
                    };
                }

                return new VariableNode { Name = name };
            }

            throw LogAndThrow($"Expected expression, got: {Current().Value}");
        }
        private bool Match(params TokenType[] types)
        {
            if (types.AsValueEnumerable().Any(Check))
            {
                Advance();
                return true;
            }
            return false;
        }
        
        private bool Check(TokenType type)
        {
            return Current().Type == type;
        }
        
        private Token Current()
        {
            return _tokens[_position];
        }
        
        private Token Previous()
        {
            return _tokens[_position - 1];
        }
        
        private Token Advance()
        {
            if (!IsAtEnd()) _position++;
            return Previous();
        }
        
        private Token Peek(int offset)
        {
            var pos = _position + offset;
            return pos < _tokens.Count ? _tokens[pos] : null;
        }
        
        private bool IsAtEnd()
        {
            return Current().Type == TokenType.EOF;
        }
        
        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) 
                return Advance();
            
            throw LogAndThrow(message);
        }

        private void SkipNewLines()
        {
            while (Match(TokenType.NewLine)) { }
        }
        
        private RiveParseException LogAndThrow(string message)
        {
            _logger.LogError($"Line {Current().Line}: {message}");
            return new RiveParseException(message, Current().Line);
        }
    }

    public enum TokenType
    {
        Number, Identifier, Operator,
        LeftParen, RightParen, LeftBrace, RightBrace,
        Equal, Comma, NewLine,
        Int, If, Else, For, To, End, Log, Return, TerminalCommand,
        Wait,
        EOF
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int Line { get; }

        public Token(TokenType type, string value, int line)
        {
            Type = type;
            Value = value;
            Line = line;
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