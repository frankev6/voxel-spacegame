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
    public enum ColliderPriority : byte
    {
        Damage = 0,   // cell changes from SPEC-13 (BrickDirtyScheduleSystem). Urgent; deterministic install tick
        Split = 1,    // first build of a grid created by SplitSystem (body existed before GridBodyInitSystem saw it). Urgent; deterministic
        Edit = 2,     // EditorPreview grid edits (SPEC-10 §5.9). Budgeted; install when built
        Spawn = 3,    // first build after spawn or rebuild. Budgeted; install when built
    }
}
