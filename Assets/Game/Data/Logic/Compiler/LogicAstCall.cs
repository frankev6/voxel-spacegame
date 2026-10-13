namespace Game.Data.Logic.Compiler
{
    internal sealed class LogicAstCall : LogicAst
    {
        public string Name { get; }
        public System.Collections.Generic.IReadOnlyList<LogicAst> Arguments { get; }
        public LogicAstCall(LogicToken token, LogicAst[] arguments) : base(token)
        { Name = token.Text; Arguments = System.Array.AsReadOnly(arguments); }
    }
}
