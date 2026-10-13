namespace Game.Data.Logic.Compiler
{
    internal abstract class LogicAst
    {
        public int Line { get; }
        public int Column { get; }
        protected LogicAst(LogicToken token) { Line = token.Line; Column = token.Column; }
    }
}
