using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using Game.Core.Components;

namespace Game.Core.Systems
{
    /// <summary>Fixed-step gameplay after the physics step (architecture §5). Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial class GameplayPostPhysicsGroup : ComponentSystemGroup
    {
        protected override void OnCreate() { base.OnCreate(); RequireForUpdate<GameplayActive>(); }
    }
}
