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
    public enum GridColliderPhase : byte
    {
        None = 0,          // no body yet (spawning, or body creation waiting for mass)
        Provisional = 1,   // body exists; provisional box installed (or the previous compound during a rebuild); first full build pending
        Ready = 2,         // a full compound built from the bricks is installed
    }
}
