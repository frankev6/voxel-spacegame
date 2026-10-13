using Game.Core.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.6: apply an origin record before parallel projectile reads and writes.</summary>
    [BurstCompile]
    internal struct ProjectileShiftJob : IJob
    {
        public NativeList<ProjectileRow> Rows;
        public NativeReference<uint> Applied;
        [ReadOnly] public ComponentLookup<OriginShiftRecord> Shifts;
        public Entity ShiftEntity;

        public void Execute()
        {
            ProjectileStore.ApplyPendingShift(ref Rows,
                Shifts.HasComponent(ShiftEntity) ? Shifts[ShiftEntity] : default, ref Applied);
        }
    }
}
