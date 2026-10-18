namespace Game.Data.Blocks
{
    /// <summary>ADR-0119 decision 7: an energy canister's payload. Unmanaged; Burst reads it.</summary>
    [System.Serializable] public struct PickupParams
    {
        public float Energy;            // energy granted to the ship that collects it (> 0)
        public float Reach;             // m from the block centre to a ship's bounding sphere surface (> 0)
    }
}
