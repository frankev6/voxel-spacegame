using Game.Galaxy.Bodies;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Presentation.World
{
    /// <summary>Render-only state. Gather snapshots root poses; Apply owns the child's local/world matrices.</summary>
    [WriteGroup(typeof(LocalToWorld))]
    internal struct CelestialVisual : IComponentData
    {
        internal Entity Anchor;
        internal float BaseScale;
        internal byte HideWithGrid;
        internal float3 Position;
        internal quaternion Rotation;
        internal float AnchorScale;
        internal BodyFlags Flags;
    }
}
