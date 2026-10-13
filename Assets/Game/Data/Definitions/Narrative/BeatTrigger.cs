namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.2 append-only beat trigger values.</summary>
    public enum BeatTrigger : byte { Interact = 0, StarSystemEntered = 1, GameStart = 2, Manual = 3, Condition = 4, Hail = 5 }
}
