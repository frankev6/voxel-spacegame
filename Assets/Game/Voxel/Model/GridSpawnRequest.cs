using Game.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4 (architecture §6.3). The entity becomes the grid root.</summary>
    public struct GridSpawnRequest : IComponentData
    {
        public ContentHash Blueprint;          // non-zero: spawn from this blueprint (read-only source, WriteBack forbidden)
        public int StructureHandle;            // ≥ 0: spawn from this registered structure; -1 = none. Exactly one source is set
        public ulong PersistentId;             // copied to the root's PersistentId; 0 only for EditorPreview grids and tests
        public RigidTransform Pose;            // local frame; position of the grid origin (cell (0,0,0) min corner)
        public float3 LinearVelocity;          // m/s, world; Dynamic only
        public float3 AngularVelocity;         // rad/s, world; Dynamic only
        public GridKind Kind;
        public GridMotion Motion;
        public GridSpawnFlags Flags;
        public GridProfile Profile;            // root component set from RootComponentRegistration (ADR-0015); None for asteroids, hulk bodies, previews
    }
}
