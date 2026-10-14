using Game.Core.Events;
using Game.Data.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Factions
{
    [BurstCompile]
    internal struct FactionApplyJob : IJob
    {
        public Entity Singleton;
        public uint Tick;
        [ReadOnly] public NativeArray<byte> Provoked;
        public BufferLookup<FactionStanding> Standings;
        public BufferLookup<FactionRelationRequest> Requests;
        public ComponentLookup<FactionState> States;
        [NativeDisableContainerSafetyRestriction] public ComponentLookup<FactionRelations> Relations;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<ManagedEventEnvelope<FactionRelationChanged>>.ParallelWriter Events;
        public void Execute()
        {
            var standings = Standings[Singleton];
            var requests = Requests[Singleton];
            var state = States[Singleton];
            var relations = Relations[Singleton];
            FixedList512Bytes<int> before = default;
            for (int i = 0; i < standings.Length; i++) before.Add(standings[i].Relation);
            foreach (var request in requests)
            {
                if (request.Faction >= state.Count) continue;
                var standing = standings[request.Faction];
                if (standing.Locked != 0) continue;
                long value = request.Mode == 0 ? (long)standing.Relation + request.Value : request.Value;
                standing.Relation = (int)(value < -100 ? -100 : value > 100 ? 100 : value);
                standings[request.Faction] = standing;
            }
            requests.Clear();
            bool changed = false;
            for (int i = 0; i < state.Count; i++)
            {
                var standing = standings[i];
                var oldStance = standing.PlayerStance;
                if (Provoked[i] != 0 && standing.Locked == 0 && oldStance != FactionStance.Hostile
                    && (standing.LastProvokedTick == 0 || Tick - standing.LastProvokedTick >= (uint)state.ProvokeIntervalTicks))
                {
                    long penalized = (long)standing.Relation - state.ProvokePenalty;
                    standing.Relation = (int)(penalized < -100 ? -100 : penalized);
                    standing.LastProvokedTick = Tick;
                }
                if (standing.Relation != before[i])
                {
                    standing.PlayerStance = standing.Relation <= state.HostileAtOrBelow ? FactionStance.Hostile
                        : standing.Relation >= state.FriendlyAtOrAbove ? FactionStance.Friendly : FactionStance.Neutral;
                    relations.Stance[i] = relations.Stance[i * state.Count] = (byte)standing.PlayerStance;
                    Events.Enqueue(new ManagedEventEnvelope<FactionRelationChanged> { Tick = Tick, SortKey = (ulong)i,
                        Payload = new FactionRelationChanged(standing.Faction, before[i], standing.Relation, (byte)oldStance, (byte)standing.PlayerStance) });
                    changed = true;
                }
                standings[i] = standing;
            }
            if (changed) { state.Revision++; States[Singleton] = state; }
        }
    }
}
