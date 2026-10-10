namespace Game.Core.Flow
{
    /// <summary>Arguments for entering the Blueprint Lab (SPEC-02 §4.8).</summary>
    public readonly struct BlueprintLabArgs
    {
        public readonly BlueprintLabMode Mode;
        public readonly ulong FleetShipId;          // FleetShip mode: PersistentId.Value of the fleet ship; 0 in Creative
        public readonly Game.Core.ContentId StartBlueprint; // Creative mode: blueprint to open; None = empty grid with a core
        public BlueprintLabArgs(BlueprintLabMode mode, ulong fleetShipId, Game.Core.ContentId startBlueprint) { Mode = mode; FleetShipId = fleetShipId; StartBlueprint = startBlueprint; }
    }
}
