using Game.Data.Blocks;
using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>A mine's fuse settings and spawn bookkeeping, on the device root (HAZ-008). Written by HazardSpawn.MineField; MineSystem
    /// records the grid revision it was armed at, so any cell lost to a hit cooks the mine off.</summary>
    public struct MineFuse : IComponentData
    {
        public ProximityFuseParams Params;
        public float DamageAtArm;    // GridDamageStats.DamageTaken when it armed (CasingHp counts from here)
        public int Cells;            // solid cells when first seen
        public uint SpawnRevision;   // VoxelGrid.Revision when first seen; 0 = not yet seen
        public ushort Group;         // owner group (a track, an encounter): HazardSpawn.DespawnGroup removes it
    }
}
