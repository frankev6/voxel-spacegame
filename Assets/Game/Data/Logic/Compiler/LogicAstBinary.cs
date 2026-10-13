namespace Game.Data.Logic.Compiler
{
    internal sealed class LogicAstBinary : LogicAst
    {
        public LogicTokenKind Operator { get; }
        public LogicAst Left { get; }
        public LogicAst Right { get; }
        public LogicAstBinary(LogicToken token, LogicAst left, LogicAst right) : base(token)
        { Operator = token.Kind; Left = left; Right = right; }
    }
}
