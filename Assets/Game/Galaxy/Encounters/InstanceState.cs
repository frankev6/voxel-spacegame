namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4 instance lifecycle state.</summary>
    public enum InstanceState : byte { Active = 0, Defeated = 1, Despawned = 2 }
}
