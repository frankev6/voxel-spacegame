using Game.Data.Blocks;
using Game.Simulation.Power;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.12: initializes only unbound weapon consumers, before PowerInitSystem binds them.</summary>
    [BurstCompile]
    internal partial struct WeaponRequestInitJob : IJobEntity
    {
        public BlobAssetReference<WeaponDefsBlob> Weapons;
        public byte Priority;
        void Execute(in FunctionalBlock functional, in WeaponMount mount, in PowerLink link, DynamicBuffer<PowerRequest> requests)
        {
            if (link.Bound != 0 || mount.Initialized == 0 || mount.WeaponIndex >= Weapons.Value.Weapons.Length) return;
            ref var weapon = ref Weapons.Value.Weapons[mount.WeaponIndex];
            PowerRegistration.SetConsumer(requests, BlockModuleId.WeaponMount, weapon.PowerDraw, Priority);
        }
    }
}
