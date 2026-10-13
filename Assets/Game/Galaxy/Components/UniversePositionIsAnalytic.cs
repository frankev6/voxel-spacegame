using Game.Galaxy.Data;
using Unity.Entities;

namespace Game.Galaxy.Components
{
    /// <summary>Tag: UniversePosition of this root is computed analytically (orbits). OriginRebaseSystem never syncs it from LocalTransform. Implements SPEC-30 §4.7.</summary>
    public struct UniversePositionIsAnalytic : IComponentData { }
}
