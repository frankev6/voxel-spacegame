namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §4.5 immutable journal fact read model.</summary>
    public readonly struct JournalEntryView { public readonly string FactName, Text; public readonly int Value;
        public JournalEntryView(string f, string t, int v) { FactName = f; Text = t; Value = v; } }
}
