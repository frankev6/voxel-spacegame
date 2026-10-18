using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>HAZ-018: the temporary gravity well (an Attract zone) a Gravity mine leaves; MineSystem removes it at UntilTick.</summary>
    public struct MineWell : IComponentData { public uint UntilTick; }
}
