using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponStats.</summary>
    public struct WeaponStats : Unity.Entities.IComponentData
    {
        public float SustainedDps;       // Σ WeaponDef.SustainedDps of alive mounts (before multipliers)
        public float MaxWeaponRange;     // m, max WeaponDef.Range of alive mounts; 0 if none (SPEC-23 reads)
        public float WeaponDraw;         // E/s at full demand
        public int AliveWeaponCount;
        public int InitialWeaponCount;   // AliveWeaponCount at the first recompute
        public uint SourceRevision;      // ShipStats.FunctionalRevision these totals were built from (0 = never)
    }
}
