using Game.Core.Systems;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>ADR-0117: the local player's ship is pilot slot 0 unless a co-op session already gave it a slot; every pilot gets its
    /// interact counter. Main thread, frame start (structural changes), every machine.</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    public partial struct PilotSetupSystem : ISystem
    {
        EntityQuery untagged, uncounted;
        public void OnCreate(ref SystemState state)
        {
            untagged = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip>().WithNone<Pilot>().Build(ref state);
            uncounted = new EntityQueryBuilder(Allocator.Temp).WithAll<Pilot>().WithNone<PilotInteractCount>().Build(ref state);
        }
        public void OnUpdate(ref SystemState state)
        {
            if (!untagged.IsEmptyIgnoreFilter) state.EntityManager.AddComponent<Pilot>(untagged);   // slot 0: the host or solo player
            if (!uncounted.IsEmptyIgnoreFilter) state.EntityManager.AddComponent<PilotInteractCount>(uncounted);
        }
    }
}
