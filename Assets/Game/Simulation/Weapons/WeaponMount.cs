using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponMount.</summary>
    public struct WeaponMount : IComponentData
    {
        public float3 PivotLocal;        // grid-local metres: footprint centre (GridSpaceMath.InstanceCenter)
        public float3 MuzzleLocal;       // grid-local metres at yaw 0: BlockPointToGrid(WeaponMountParams.MuzzleOffsetCells) (SPEC-04 §4.6 row)
        public float3 ForwardLocal;      // grid-local unit: BlockDirToGrid(+Z)
        public float3 UpLocal;           // grid-local unit: BlockDirToGrid(+Y)
        public ushort WeaponIndex;       // WeaponMountParams.WeaponIndex; 0 = unarmed (never fires)
        public byte Initialized;         // 1 after WeaponInitSystem filled it
        public byte Reserved;
    }
}
