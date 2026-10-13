using Game.Galaxy.Data;
using Unity.Entities;

namespace Game.Galaxy.Components
{
    /// <summary>Shared, low cardinality (≤ 2 values). On every star-system-owned entity: grid roots, bricks, functional entities, body anchors. Never on the player ship or its children. Implements SPEC-30 §4.7.</summary>
    public struct StarSystemMember : ISharedComponentData { public StarSystemId Id; }
}
