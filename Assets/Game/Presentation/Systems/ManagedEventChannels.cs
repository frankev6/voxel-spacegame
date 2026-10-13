using Game.Core.Events;
using Game.Data.Components;
using Unity.Entities;
[assembly: RegisterGenericComponentType(typeof(ManagedEventQueue<StarSystemEntered>))]

namespace Game.Presentation.Systems
{
    /// <summary>The one list of ECS-produced managed event types. Producing specs add one line each (SPEC-02 §4.6, §5.6).</summary>
    public static class ManagedEventChannels
    {
        public static void RegisterAll(ManagedEventDrainSystem system) { system.RegisterChannel<StarSystemEntered>(); system.RegisterChannel<ShipDestroyed>(); system.RegisterChannel<OriginShifted>(); system.RegisterChannel<BodyInteracted>(); system.RegisterChannel<FactionRelationChanged>(); }
    }
}
