using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Disables only the named split pair while either body's grace is active (SPEC-12 §5.12).</summary>
    [BurstCompile]
    internal struct SplitGraceBodyPairsJob : IBodyPairsJob
    {
        [ReadOnly] public ComponentLookup<SplitGrace> Grace;
        public uint Tick;

        public void Execute(ref ModifiableBodyPair pair)
        {
            if (Suppressed(pair.EntityA, pair.EntityB) || Suppressed(pair.EntityB, pair.EntityA)) pair.Disable();
        }

        bool Suppressed(Entity body, Entity other) =>
            Grace.HasComponent(body) && Grace[body].Other == other && Tick < Grace[body].UntilTick;
    }
}
