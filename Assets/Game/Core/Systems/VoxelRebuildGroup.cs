using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using Game.Core.Components;

namespace Game.Core.Systems
{
    /// <summary>Variable-rate mesh and collider rebuilds after the fixed step (architecture §5). Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class VoxelRebuildGroup : ComponentSystemGroup { }
}
