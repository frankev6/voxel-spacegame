namespace Game.Simulation.Abilities
{
    /// <summary>SPEC-24 §5.2: why a slot is Blocked. AbilitySlot.Blocked holds one of these; any non-zero value blocks activation, and the
    /// HUD names the reason. Effect systems with a single reason write Unavailable.</summary>
    public static class AbilityBlockReason
    {
        public const byte None = 0;
        public const byte Unavailable = 1;
        public const byte NoDesign = 2;          // ABL-006: no design to rebuild towards
        public const byte NoHullPlating = 3;     // ABL-006: a pilot with no hull items in the hold
        public const byte NothingToRepair = 4;   // ABL-006: nothing missing or destroyed

        /// <summary>Short HUD text for the reason, or "" for none.</summary>
        public static string Text(byte reason) => reason switch
        {
            None => "",
            NoDesign => "No design to rebuild",
            NoHullPlating => "No hull plating",
            NothingToRepair => "Nothing to repair",
            _ => "Unavailable"
        };
    }
}
