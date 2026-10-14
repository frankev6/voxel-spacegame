using System.Collections.Generic;

namespace Game.Editor.Narrative.Dialog
{
    public sealed class DialogNodeAst
    {
        public string Title;
        public string[] Tags = System.Array.Empty<string>();
        public int Line;
        public readonly List<DialogStatementAst> Body = new();
    }
}
