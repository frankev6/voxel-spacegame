using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using Game.Core.Components;

namespace Game.Core.Systems
{
    /// <summary>Ability activation inside the pre-physics step (architecture §5). Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    public partial class AbilityActivationGroup : ComponentSystemGroup { }   // ordering set by members' neighbours, see §5.8
}
