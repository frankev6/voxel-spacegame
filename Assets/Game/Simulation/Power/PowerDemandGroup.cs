using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. Demand writers run after ShipCommandGroup (OrderFirst), before PowerAllocationSystem.
    /// Members order themselves only relative to each other if needed; they never touch another module's element.</summary>
    [UpdateInGroup(typeof(Game.Core.Systems.GameplayPrePhysicsGroup))]
    [UpdateBefore(typeof(PowerAllocationSystem))]
    public partial class PowerDemandGroup : ComponentSystemGroup { }
}
