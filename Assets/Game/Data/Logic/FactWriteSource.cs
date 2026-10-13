namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    public enum FactWriteSource : byte { Effect = 0, Dialog = 1, Quest = 2, Clue = 3, Beat = 4, WorldEvent = 5, NewGame = 6, Debugger = 7 }
}
