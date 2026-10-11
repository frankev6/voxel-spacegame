using Game.Voxel.Model;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6.</summary>
    public struct GridSpawnParams
    { public RigidTransform Pose; public float3 LinearVelocity, AngularVelocity; public GridKind Kind; public GridMotion Motion; public GridSpawnFlags Flags; public GridProfile Profile; public ulong PersistentId; }
}
