namespace Game.Data.Logic.Compiler
{
    internal readonly struct LogicToken
    {
        public readonly LogicTokenKind Kind;
        public readonly string Text;
        public readonly int Line, Column;
        public LogicToken(LogicTokenKind kind, string text, int line, int column)
        { Kind = kind; Text = text; Line = line; Column = column; }
    }
}
