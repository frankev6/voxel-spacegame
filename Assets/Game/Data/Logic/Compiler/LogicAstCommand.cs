namespace Game.Data.Logic.Compiler
{
    internal sealed class LogicAstCommand : LogicAst
    {
        public string Name { get; }
        public System.Collections.Generic.IReadOnlyList<LogicAst> Arguments { get; }
        public LogicAstCommand(LogicToken token, LogicAst[] arguments) : base(token)
        { Name = token.Text; Arguments = System.Array.AsReadOnly(arguments); }
    }
}
