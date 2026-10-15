using Unity.Entities;

namespace Game.Galaxy.Interaction
{
    /// <summary>ADR-0117: each pilot's interaction target, derived on every machine by InteractionQuerySystem. The local pilot's is also
    /// the <see cref="InteractionTarget"/> singleton that presentation reads.</summary>
    public struct PilotInteraction : IComponentData { public InteractionTarget Value; }

    /// <summary>The Interact presses of this pilot that InteractionTriggerSystem (authority) has already handled.</summary>
    public struct PilotInteractSeen : IComponentData { public byte Value; public byte Initialized; }
}
