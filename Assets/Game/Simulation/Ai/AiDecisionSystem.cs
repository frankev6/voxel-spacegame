using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Abilities;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Simulation.Ai
{
    /// <summary>Schedules deterministic staggered decisions and owns query warning lifetime (SPEC-23 §5.4).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ShipCommandGroup))]
    [UpdateAfter(typeof(AiAnchorShiftSystem))]
    public partial struct AiDecisionSystem : ISystem
    {
        EntityStorageInfoLookup entities;
        ComponentLookup<Hulk> hulks;
        ComponentLookup<GridSpawning> spawning;
        ComponentLookup<Dormant> dormant;
        ComponentLookup<ShipStats> shipStats;
        ComponentLookup<WeaponStats> weaponStats;
        ComponentLookup<ShieldState> shields;
        ComponentLookup<ShieldControl> shieldControls;
        BufferLookup<AbilitySlot> abilities;
        NativeArray<int> overflow;
        ComponentLookup<FleetMember> members;
        NativeHashMap<Entity, FleetLeaderDecision> leaders;
        internal int SphereQueryCount => overflow[2];

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<AiConfig>();
            state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<SimClock>();
            entities = state.GetEntityStorageInfoLookup();
            hulks = state.GetComponentLookup<Hulk>(true);
            spawning = state.GetComponentLookup<GridSpawning>(true);
            dormant = state.GetComponentLookup<Dormant>(true);
            shipStats = state.GetComponentLookup<ShipStats>(true);
            weaponStats = state.GetComponentLookup<WeaponStats>(true);
            shields = state.GetComponentLookup<ShieldState>(true);
            shieldControls = state.GetComponentLookup<ShieldControl>(true);
            abilities = state.GetBufferLookup<AbilitySlot>(true);
            overflow = new NativeArray<int>(3, Allocator.Persistent);
            members = state.GetComponentLookup<FleetMember>(true);
            leaders = new NativeHashMap<Entity, FleetLeaderDecision>(128, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            if (overflow.IsCreated) overflow.Dispose();
            if (leaders.IsCreated) leaders.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            entities.Update(ref state); hulks.Update(ref state); spawning.Update(ref state);
            dormant.Update(ref state); shipStats.Update(ref state); weaponStats.Update(ref state);
            shields.Update(ref state); shieldControls.Update(ref state); abilities.Update(ref state);
            members.Update(ref state);
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            var think = new AiThinkJob
            {
                Hash = hash.AsReadOnly(), Config = SystemAPI.GetSingleton<AiConfig>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(), Tick = SystemAPI.GetSingleton<SimClock>().Tick,
                Entities = entities, Hulks = hulks, Spawning = spawning, Dormant = dormant,
                ShipStats = shipStats, WeaponStats = weaponStats, Shields = shields,
                ShieldControls = shieldControls, Abilities = abilities, Overflow = overflow,
                Members = members, Leaders = leaders, MemberPass = 0,
            };
            var handle = think.ScheduleParallel(hash.BeginRead(state.Dependency));
            handle = new ClearFleetLeaders { Leaders = leaders }.Schedule(handle);
            handle = new CaptureFleetLeaders { Leaders = leaders }.Schedule(handle);
            think.MemberPass = 1;
            handle = think.ScheduleParallel(handle);
            hash.EndRead(handle);
            state.Dependency = handle;
        }
    }
}
