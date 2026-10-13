using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. Demand writers run after ShipCommandGroup (OrderFirst), before PowerAllocationSystem.
    /// Members order themselves only relative to each other if needed; they never touch another module's element.</summary>
    [UpdateInGroup(typeof(Game.Core.Systems.GameplayPrePhysicsGroup))]
    // TODO(PWR-005): add UpdateBefore(typeof(PowerAllocationSystem)) when the allocator exists (SPEC-22 Q10).
    // Its specified UpdateAfter(typeof(PowerDemandGroup)) preserves the same ordering edge.
    public partial class PowerDemandGroup : ComponentSystemGroup { }
}
