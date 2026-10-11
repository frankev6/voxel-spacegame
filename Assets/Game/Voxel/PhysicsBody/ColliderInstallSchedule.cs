using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public static class ColliderInstallSchedule
    {
        public static bool IsUrgent(ColliderPriority p) => p <= ColliderPriority.Split;
        /// Urgent: RoundUp(requestTick + delay, bucket); otherwise 0 (= install when built).
        public static uint InstallTick(ColliderPriority p, uint requestTick, int delay, int bucket) => throw new System.NotImplementedException("PHY-009");
        /// True when no request made at a tick ≥ currentTick can map to installTick (the update may be sealed).
        public static bool IsWindowClosed(uint installTick, uint currentTick, int delay) => throw new System.NotImplementedException("PHY-009");   // installTick == 0 || currentTick > installTick - delay
        public static uint RoundUp(uint value, uint multiple) => throw new System.NotImplementedException("PHY-009");                              // ((value + multiple − 1) / multiple) × multiple
    }
}
