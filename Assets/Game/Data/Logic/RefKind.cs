namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.4); later task implementations are explicitly labelled.</summary>
    public enum RefKind : byte { Item = 0, Entity = 1, StarSystem = 2, Quest = 3, Clue = 4, Beat = 5, Faction = 6, Mystery = 7, LootTable = 8, Merchant = 9, Encounter = 10, Blueprint = 11, AudioCue = 12 }
}
