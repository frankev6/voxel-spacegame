namespace Game.Data.Logic.Compiler
{
    internal sealed class LogicAstUnary : LogicAst
    {
        public LogicTokenKind Operator { get; }
        public LogicAst Operand { get; }
        public LogicAstUnary(LogicToken token, LogicAst operand) : base(token)
        { Operator = token.Kind; Operand = operand; }
    }
}
