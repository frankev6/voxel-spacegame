using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using Game.Core.Components;

namespace Game.Core.Systems
{
    /// <summary>Initialization work after Unity's own (architecture §5). Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndInitializationEntityCommandBufferSystem))]
    public partial class GameInitGroup : ComponentSystemGroup { }
}
