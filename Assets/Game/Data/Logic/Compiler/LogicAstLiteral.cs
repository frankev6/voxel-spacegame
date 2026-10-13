namespace Game.Data.Logic.Compiler
{
    internal sealed class LogicAstLiteral : LogicAst
    {
        public LogicTokenKind Kind { get; }
        public object Value { get; }
        public LogicAstLiteral(LogicToken token, object value) : base(token)
        { Kind = token.Kind; Value = value; }
    }
}
