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
    public static class ColliderDebug
    {
        /// Main thread, debug only (unsafe walk of the installed blob). Appends one entry per box leaf; returns the count; 0 if the root has no collider.
        public static int CollectBoxes(EntityManager em, Entity root, NativeList<ColliderDebugBox> output) => throw new System.NotImplementedException("PHY-015");
    }
}
