using Unity.Entities;

namespace Game.Simulation.Mechanisms
{
    /// <summary>On a turret group root (ADR-0119 decision 6): a TargetPolicy action overrides whom its turrets shoot.</summary>
    public struct TargetOverride : IComponentData
    {
        public Entity Target;
        public byte Policy;             // 0 none (normal targeting), 1 the race leader, 2 the fixed Target
    }
}
