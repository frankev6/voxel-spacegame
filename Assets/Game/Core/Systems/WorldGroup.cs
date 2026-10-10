using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using Game.Core.Components;

namespace Game.Core.Systems
{
    /// <summary>Variable-rate world services: streaming, travel, encounters, interaction, LOD (architecture §5). Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelRebuildGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class WorldGroup : ComponentSystemGroup
    {
        protected override void OnCreate() { base.OnCreate(); RequireForUpdate<GameplayActive>(); }
    }
}
