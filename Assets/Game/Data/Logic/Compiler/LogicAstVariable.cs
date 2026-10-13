namespace Game.Data.Logic.Compiler
{
    internal sealed class LogicAstVariable : LogicAst
    {
        public string Name { get; }
        public LogicAstVariable(LogicToken token) : base(token) { Name = token.Text; }
    }
}
