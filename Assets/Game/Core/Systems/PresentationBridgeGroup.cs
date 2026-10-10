using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using Game.Core.Components;

namespace Game.Core.Systems
{
    /// <summary>Bridges from simulation to the player: meshes, VFX, audio, camera, HUD, managed events (architecture §5).
    /// Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BeginPresentationEntityCommandBufferSystem))]
    public partial class PresentationBridgeGroup : ComponentSystemGroup { }
}
