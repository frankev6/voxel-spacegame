using Game.Core.Systems;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>ADR-0117: the local player's ship is pilot slot 0 unless a co-op session already gave it a slot; every pilot gets its
    /// interact counter and combat status. Main thread, frame start (structural changes), every machine.</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    public partial struct PilotSetupSystem : ISystem
    {
        EntityQuery untagged, uncounted, unstatused;
        public void OnCreate(ref SystemState state)
        {
            untagged = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip>().WithNone<Pilot>().Build(ref state);
            uncounted = new EntityQueryBuilder(Allocator.Temp).WithAll<Pilot>().WithNone<PilotInteractCount>().Build(ref state);
            unstatused = new EntityQueryBuilder(Allocator.Temp).WithAll<Pilot>().WithNone<Game.Simulation.Ai.PilotCombatStatus>().Build(ref state);
        }
        public void OnUpdate(ref SystemState state)
        {
            if (!untagged.IsEmptyIgnoreFilter) state.EntityManager.AddComponent<Pilot>(untagged);   // slot 0: the host or solo player
            if (!uncounted.IsEmptyIgnoreFilter) state.EntityManager.AddComponent<PilotInteractCount>(uncounted);
            if (!unstatused.IsEmptyIgnoreFilter) state.EntityManager.AddComponent<Game.Simulation.Ai.PilotCombatStatus>(unstatused);
        }
    }
}
