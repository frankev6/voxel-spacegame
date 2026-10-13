namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.3. Slot lifecycle; SPEC-22 reads PendingPower.</summary>
    public enum AbilitySlotState : byte { Ready = 0, PendingPower = 1, Active = 2, Cooldown = 3, Unavailable = 4 }
}
