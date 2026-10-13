namespace Game.Narrative.Dialog
{
    /// <summary>Runtime command lookup built at boot from DialogCommandTableDefinition. Implements SPEC-41 §5.5.</summary>
    public sealed class DialogCommandTable
    {
        public DialogCommandTable(Game.Data.Definitions.DialogCommandTableDefinition definition) => throw new System.NotImplementedException("DLG-006");
        public bool TryGet(string normalizedText, out Game.Data.Logic.IEffect effect) => throw new System.NotImplementedException("DLG-006");   // Dictionary<string, IEffect>, ordinal
        public int Count { get; }
        // TODO(DLG-006): implement SPEC-41 service, command lookup and visit memory behavior.
    }
}
