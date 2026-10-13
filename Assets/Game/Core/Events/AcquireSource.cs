namespace Game.Core.Events
{
    /// <summary>SPEC-50 §4.2: acquisition provenance; append only.</summary>
    public enum AcquireSource : byte { Narrative = 0, Loot = 1, Purchase = 2, Salvage = 3, EditorRefund = 4, Dev = 5, NewGame = 6 }
}
