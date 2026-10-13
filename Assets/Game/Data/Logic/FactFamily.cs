namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    public enum FactFamily : byte { Authored = 0, Beat = 1, Clue = 2, Mystery = 3, Visited = 4, Interacted = 5, Destroyed = 6, Defeated = 7, Acquired = 8, System = 9 }
}
