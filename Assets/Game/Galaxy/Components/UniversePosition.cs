using Game.Galaxy.Data;
using Unity.Entities;

namespace Game.Galaxy.Components
{
    /// <summary>Star-system-frame position (architecture §6.5). On every persistent root entity. Synced by OriginRebaseSystem. Implements SPEC-30 §4.7.</summary>
    public struct UniversePosition : IComponentData { public Unity.Mathematics.double3 Value; }
}
