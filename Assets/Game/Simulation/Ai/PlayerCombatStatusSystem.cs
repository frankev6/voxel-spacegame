using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Ai
{
    /// <summary>SPEC-23 §5.6: owns the boot-lifetime combat status and schedules one guarded count after steering.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ShipCommandGroup))]
    [UpdateAfter(typeof(AiSteeringSystem))]
    public partial struct PlayerCombatStatusSystem : ISystem
    {
        Entity singleton;
        EntityQuery players;
        EntityStorageInfoLookup entities;
        ComponentLookup<AiBrain> brains;
        ComponentLookup<ShipTarget> targets;
        ComponentLookup<Allegiance> allegiances;
        ComponentLookup<Hulk> hulks;
        ComponentLookup<GridSpawning> spawning;
        ComponentLookup<GridDamageStats> damage;
        ComponentLookup<PlayerCombatStatus> statuses;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Created before SceneFlow snapshots boot entities; never create a transient gameplay singleton.
            singleton = state.EntityManager.CreateSingleton(default(PlayerCombatStatus), "PlayerCombatStatus");
            players = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip>().Build(ref state);
            entities = state.GetEntityStorageInfoLookup();
            brains = state.GetComponentLookup<AiBrain>(true);
            targets = state.GetComponentLookup<ShipTarget>(true);
            allegiances = state.GetComponentLookup<Allegiance>(true);
            hulks = state.GetComponentLookup<Hulk>(true);
            spawning = state.GetComponentLookup<GridSpawning>(true);
            damage = state.GetComponentLookup<GridDamageStats>(true);
            statuses = state.GetComponentLookup<PlayerCombatStatus>();
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<AiConfig>();
            state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            entities.Update(ref state); brains.Update(ref state); targets.Update(ref state); allegiances.Update(ref state);
            hulks.Update(ref state); spawning.Update(ref state); damage.Update(ref state); statuses.Update(ref state);
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            var playerList = players.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var gathered);
            var counted = new CountJob
            {
                Players = playerList, Hash = hash.AsReadOnly(), Relations = SystemAPI.GetSingleton<FactionRelations>(),
                BubbleRadius = SystemAPI.GetSingleton<AiConfig>().CombatBubbleRadius, Singleton = singleton,
                Entities = entities, Brains = brains, Targets = targets, Allegiances = allegiances,
                Hulks = hulks, Spawning = spawning, Damage = damage, Statuses = statuses
            }.Schedule(JobHandle.CombineDependencies(gathered, hash.BeginRead(state.Dependency)));
            hash.EndRead(counted);
            state.Dependency = playerList.Dispose(counted);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            if (state.EntityManager.Exists(singleton)) state.EntityManager.DestroyEntity(singleton);
        }

        [BurstCompile]
        struct CountJob : IJob
        {
            [ReadOnly] public NativeList<Entity> Players;
            [ReadOnly] public SpatialHashQueryData Hash;
            [ReadOnly] public FactionRelations Relations;
            public float BubbleRadius;
            public Entity Singleton;
            [ReadOnly] public EntityStorageInfoLookup Entities;
            [ReadOnly] public ComponentLookup<AiBrain> Brains;
            [ReadOnly] public ComponentLookup<ShipTarget> Targets;
            [ReadOnly] public ComponentLookup<Allegiance> Allegiances;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            [ReadOnly] public ComponentLookup<GridDamageStats> Damage;
            public ComponentLookup<PlayerCombatStatus> Statuses;

            public void Execute()
            {
                if (!Statuses.HasComponent(Singleton)) return;
                var status = Statuses[Singleton]; status.HostilesEngaging = 0; status.Reserved = 0;
                // Multiple player tags are an invalid/ambiguous scene; never choose by chunk order.
                if (Players.Length != 1) { Statuses[Singleton] = status; return; }
                Entity player = Players[0];
                if (!Entities.Exists(player)) { Statuses[Singleton] = status; return; }
                status.LastDamagedTick = Damage.HasComponent(player) ? Damage[player].LastDamageTick : 0;
                int playerIndex = Hash.FindIndex(player);
                if (playerIndex < 0 || !math.isfinite(BubbleRadius) || BubbleRadius < 0 || !Allegiances.HasComponent(player))
                { Statuses[Singleton] = status; return; }
                float3 centre = Hash.Get(playerIndex).Center;
                if (!math.all(math.isfinite(centre))) { Statuses[Singleton] = status; return; }
                var playerAllegiance = Allegiances[player]; int count = 0;
                // Scan once per player status, rather than truncate a fixed-capacity sphere query. Distance is centre-to-centre.
                for (int i = 0; i < Hash.Proxies.Length; i++)
                {
                    var proxy = Hash.Get(i); Entity root = proxy.Entity;
                    if (root == player || (proxy.Kind != ProxyKind.Ship && proxy.Kind != ProxyKind.Station) || !Entities.Exists(root) ||
                        !Brains.HasComponent(root) || Brains[root].State != AiState.Engage || !Targets.HasComponent(root) || Targets[root].Value != player ||
                        Hulks.HasComponent(root) || (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) ||
                        !Allegiances.HasComponent(root) || !Relations.IsHostile(Allegiances[root], playerAllegiance) || !math.all(math.isfinite(proxy.Center))) continue;
                    double3 delta = (double3)proxy.Center - (double3)centre;
                    if (math.lengthsq(delta) <= (double)BubbleRadius * BubbleRadius) count++;
                }
                status.HostilesEngaging = (ushort)math.min(count, ushort.MaxValue);
                Statuses[Singleton] = status;
            }
        }
    }
}
