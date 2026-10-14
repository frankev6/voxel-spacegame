using System.Collections.Generic;

namespace Game.Editor.Narrative.Dialog
{
    /// <summary>Editor-only syntax nodes; text is unescaped, expressions and commands retain authored spelling.</summary>
    public abstract class DialogStatementAst { public int Line; }
    public sealed class LineStmt : DialogStatementAst
    {
        public string Text, SpeakerName = "", LineId = "";
        // Tags omit the leading '#'; semantic tag validation belongs to DLG-003.
        public string[] Tags = System.Array.Empty<string>();
    }
    public sealed class OptionGroupStmt : DialogStatementAst { public readonly List<OptionStmt> Options = new(); }
    public sealed class OptionStmt : DialogStatementAst
    {
        public string Text, LineId = "", Condition = "";
        public string[] Tags = System.Array.Empty<string>();
        public readonly List<DialogStatementAst> Body = new();
    }
    public sealed class CommandStmt : DialogStatementAst { public string Text; }
    public sealed class IfStmt : DialogStatementAst
    {
        public readonly List<DialogIfBranch> Branches = new();
        public readonly List<DialogStatementAst> Else = new();
        public bool HasElse;
    }
    public sealed class DialogIfBranch
    {
        public string Condition;
        public readonly List<DialogStatementAst> Body = new();
    }
    public sealed class JumpStmt : DialogStatementAst { public string Node; }
    public sealed class DetourStmt : DialogStatementAst { public string Node; }
    public sealed class ReturnStmt : DialogStatementAst { }
    public sealed class StopStmt : DialogStatementAst { }
    public sealed class SetStmt : DialogStatementAst { public string Fact, Expression; }
    public sealed class CommentStmt : DialogStatementAst { public string Text; }
}
