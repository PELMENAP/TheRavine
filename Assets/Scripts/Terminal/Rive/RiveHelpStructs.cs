using System.Collections.Generic;

namespace TheRavine.Base
{
    public abstract class AstNode
    {
        public int Line { get; set; }
    }
    public abstract class ExpressionNode : AstNode { }

    public class LiteralNode : ExpressionNode
    {
        public int Value { get; set; }
    }

    public class VariableNode : ExpressionNode
    {
        public string Name { get; set; }
    }

    public class BinaryOpNode : ExpressionNode
    {
        public ExpressionNode Left { get; set; }
        public ExpressionNode Right { get; set; }
        public BinaryOperator Operator { get; set; }
    }

    public enum BinaryOperator
    {
        Add, Subtract, Multiply, Divide,
        Greater, Less, GreaterOrEqual, LessOrEqual, Equal, NotEqual
    }

    public class FunctionCallNode : ExpressionNode
    {
        public string FunctionName { get; set; }
        public List<ExpressionNode> Arguments { get; set; } = new();
    }
    public abstract class StatementNode : AstNode { }

    public class VariableDeclarationNode : StatementNode
    {
        public string Name { get; set; }
        public ExpressionNode InitialValue { get; set; }
    }

    public class AssignmentNode : StatementNode
    {
        public string VariableName { get; set; }
        public ExpressionNode Value { get; set; }
    }

    public class ReturnNode : StatementNode
    {
        public ExpressionNode Value { get; set; }
    }

    public class IfNode : StatementNode
    {
        public ExpressionNode Condition { get; set; }
        public List<StatementNode> ThenBlock { get; set; } = new();
        public List<StatementNode> ElseBlock { get; set; } = new();
    }

    public class ForLoopNode : StatementNode
    {
        public string VariableName { get; set; }
        public ExpressionNode StartExpression { get; set; }
        public ExpressionNode EndExpression { get; set; }
        public List<StatementNode> Body { get; set; } = new();
    }

    public class TerminalCommandNode : StatementNode
    {
        public string Command { get; set; }
        public List<string> VariableReferences { get; set; } = new();
    }

    public class LogNode : StatementNode
    {
        public ExpressionNode Expression { get; set; }
        public string OriginalExpression { get; set; }
    }

    public class WaitNode : StatementNode
    {
        public ExpressionNode Milliseconds { get; set; }
    }

    public class ProgramNode : AstNode
    {
        public string Name { get; set; }
        public List<string> Parameters { get; set; } = new();
        public List<StatementNode> Statements { get; set; } = new();
    }
}