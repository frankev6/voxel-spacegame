using System;

namespace Game.Data.Logic.Compiler
{
    /// <summary>First-error parser diagnostic; converted to validation reports by NAR-003.</summary>
    internal sealed class LogicDiagnostics : Exception
    {
        public string Code { get; }
        public int Line { get; }
        public int Column { get; }
        public LogicDiagnostics(string code, string message, int line, int column) : base(message)
        { Code = code; Line = line; Column = column; }
    }
}
