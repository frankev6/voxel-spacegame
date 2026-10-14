using System.Collections.Generic;
using Game.Data.Validation;

namespace Game.Editor.Narrative.Dialog
{
    /// <summary>SPEC-41 §5.1 parsed source; diagnostics retain original file and one-based line numbers.</summary>
    public sealed class DialogScriptAst
    {
        public string Path;
        public readonly List<DialogNodeAst> Nodes = new();
        public readonly List<CommandStmt> GeneratedDeclarations = new();
        public readonly ValidationReport Diagnostics = new ValidationReport("Dialog parser");
        public bool HasErrors => Diagnostics.HasErrors;
    }
}
