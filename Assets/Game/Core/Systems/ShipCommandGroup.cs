using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using Game.Core.Components;

namespace Game.Core.Systems
{
    /// <summary>Player and AI control → ShipCommand, first in the pre-physics step (architecture §5). Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup), OrderFirst = true)]
    public partial class ShipCommandGroup : ComponentSystemGroup { }
}
