using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Presentation.Components;
using Game.Presentation.Jobs;
using Game.Simulation;
using Game.Simulation.Abilities;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Presentation.Systems
{
    /// <summary>SPEC-61 §5.1: sole producer of the unmanaged HUD snapshot and bounded radar buffer.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(CameraTargetSyncSystem))]
    public partial struct HudSnapshotSystem : ISystem
    {
        static readonly ProfilerMarker ShipMarker = new ProfilerMarker(ProfilerCategory.Scripts, "HudSnapshotSystem.Ship");
        static readonly ProfilerMarker WeaponsMarker = new ProfilerMarker(ProfilerCategory.Scripts, "HudSnapshotSystem.Weapons");
        static readonly ProfilerMarker RadarMarker = new ProfilerMarker(ProfilerCategory.Scripts, "HudSnapshotSystem.Radar");
        Entity snapshot;
        EntityQuery players, weapons, grids, bodies, points, bodyPoints;
        Entity lastLivePlayer;
        HudSnapshot lastLiveMeters;
        ComponentTypeHandle<FunctionalBlock> blockHandle;
        ComponentTypeHandle<WeaponState> weaponHandle;
        ComponentTypeHandle<LocalToWorld> transformHandle;
        EntityTypeHandle entityHandle;
        ComponentLookup<VoxelGrid> gridLookup;
        ComponentLookup<Allegiance> allegianceLookup;
        ComponentLookup<HudSnapshot> snapshotLookup;
        BufferLookup<HudRadarBlip> radarLookup;
        // Internal clock seam for SPEC-61 simulated-frame acceptance; production uses unscaled engine time.
        internal bool UseTestClock;
        internal double TestElapsed;
        internal uint TestFrame;
        double lastRadar;
        uint radarVersion;
        FactionRelations neutralRelations;

        public void OnCreate(ref SystemState state)
        {
            snapshot = state.EntityManager.CreateEntity(typeof(HudSnapshot));
            var radar = state.EntityManager.AddBuffer<HudRadarBlip>(snapshot); radar.Capacity = 64;
            state.RequireForUpdate<GameplayActive>();
            players = SystemAPI.QueryBuilder().WithAll<PlayerShip>().Build();
            weapons = SystemAPI.QueryBuilder().WithAll<FunctionalBlock, WeaponState>().Build();
            grids = SystemAPI.QueryBuilder().WithAll<VoxelGrid, Allegiance, LocalToWorld>().WithNone<PlayerShip>().Build();
            bodies = SystemAPI.QueryBuilder().WithAll<StarSystemBody, SpatialBody, LocalToWorld>().WithNone<VoxelGrid, PlayerShip>().Build();
            points = SystemAPI.QueryBuilder().WithAll<Interactable, LocalToWorld>().WithNone<VoxelGrid, StarSystemBody, PlayerShip>().Build();
            bodyPoints = SystemAPI.QueryBuilder().WithAll<Interactable, StarSystemBody, LocalToWorld>().WithNone<VoxelGrid, SpatialBody, PlayerShip>().Build();
            blockHandle = state.GetComponentTypeHandle<FunctionalBlock>(true);
            weaponHandle = state.GetComponentTypeHandle<WeaponState>(true);
            transformHandle = state.GetComponentTypeHandle<LocalToWorld>(true);
            entityHandle = state.GetEntityTypeHandle();
            gridLookup = state.GetComponentLookup<VoxelGrid>(true);
            allegianceLookup = state.GetComponentLookup<Allegiance>(true);
            snapshotLookup = state.GetComponentLookup<HudSnapshot>();
            radarLookup = state.GetBufferLookup<HudRadarBlip>();
            lastRadar = double.NegativeInfinity;
            neutralRelations = new FactionRelations { Stance = new NativeArray<byte>(0, Allocator.Persistent) };
        }

        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            neutralRelations.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            int playerCount = players.CalculateEntityCount();
            if (playerCount != 1)
            {
                if (playerCount > 1) GameLog.WarnOnce("HUD.MultiplePlayers", "UI", "Multiple player ships; hiding ship HUD until exactly one remains.");
                lastLivePlayer = Entity.Null;
                var empty = SystemAPI.GetComponent<HudSnapshot>(snapshot);
                empty.Flags = HudFlags.None;
                SystemAPI.SetComponent(snapshot, empty);
                SystemAPI.GetBuffer<HudRadarBlip>(snapshot).Clear();
                lastRadar = double.NegativeInfinity;
                return;
            }
            var player = players.GetSingletonEntity();
            var value = new HudSnapshot { Flags = HudFlags.HasShip, TargetFaction = 255, RadarVersion = radarVersion,
                FrameIndex = UseTestClock ? TestFrame : (uint)UnityEngine.Time.frameCount };
            float3 position = 0;
            byte faction = 255;
            Entity target = Entity.Null;
            using (ShipMarker.Auto())
            {
                if (SystemAPI.HasComponent<LocalToWorld>(player)) position = SystemAPI.GetComponent<LocalToWorld>(player).Position;
                if (SystemAPI.HasComponent<Allegiance>(player)) faction = SystemAPI.GetComponent<Allegiance>(player).Faction;
                if (SystemAPI.HasComponent<Hulk>(player)) value.Flags |= HudFlags.IsHulk;
                if (SystemAPI.HasComponent<ShipStats>(player)) value.Hull01 = SystemAPI.GetComponent<ShipStats>(player).MassFraction;
                if (SystemAPI.HasComponent<ShieldState>(player))
                {
                    var shield = SystemAPI.GetComponent<ShieldState>(player);
                    value.ShieldHp = shield.Hp; value.ShieldMax = shield.MaxHp;
                    value.ShieldStatus = (byte)(shield.MaxHp == 0 ? 0 : shield.Active != 0 ? 1 : (shield.Flags & ShieldFlags.Collapsed) != 0 ? 2 : 3);
                }
                if (SystemAPI.HasComponent<ShipPower>(player))
                {
                    var power = SystemAPI.GetComponent<ShipPower>(player); value.Energy = power.Stored; value.EnergyMax = power.Capacity;
                }
                if (SystemAPI.HasComponent<PhysicsVelocity>(player)) value.SpeedMps = math.length(SystemAPI.GetComponent<PhysicsVelocity>(player).Linear);
                if (SystemAPI.HasComponent<ShipCommand>(player) && (SystemAPI.GetComponent<ShipCommand>(player).Flags & ShipCommandFlags.Boost) != 0)
                    value.Flags |= HudFlags.Boosting;
                if (SystemAPI.HasBuffer<AbilitySlot>(player))
                {
                    var slots = SystemAPI.GetBuffer<AbilitySlot>(player);
                    for (int i = 0; i < math.min(4, slots.Length); i++)
                    {
                        var slot = slots[i];
                        value.Abilities.Add(new HudAbilityState { Ability = slot.Ability, Charges = slot.Charges, Active = slot.Active,
                            Cooldown01 = slot.CooldownRemainingTicks / math.max(1, slot.CooldownTicks),
                            Ready = (byte)(slot.CooldownRemainingTicks <= 0 && slot.Charges > 0 ? 1 : 0) });
                    }
                }
                if (SystemAPI.HasComponent<ShipTarget>(player)) target = SystemAPI.GetComponent<ShipTarget>(player).Value;
                if (target != Entity.Null && state.EntityManager.Exists(target))
                {
                    value.Flags |= HudFlags.HasTarget;
                    if (SystemAPI.HasComponent<Allegiance>(target)) value.TargetFaction = SystemAPI.GetComponent<Allegiance>(target).Faction;
                    if (SystemAPI.TryGetSingleton<FactionRelations>(out var relations)) value.TargetRelation = (byte)relations.Get(faction, value.TargetFaction);
                    if (SystemAPI.HasComponent<ShipStats>(target)) value.TargetHull01 = SystemAPI.GetComponent<ShipStats>(target).MassFraction;
                    if (SystemAPI.HasComponent<ShieldState>(target))
                    {
                        var shield = SystemAPI.GetComponent<ShieldState>(target);
                        value.TargetShield01 = shield.MaxHp > 0 ? shield.Hp / shield.MaxHp : 0;
                    }
                    if (SystemAPI.HasComponent<LocalToWorld>(target)) value.TargetDistanceM = math.distance(position, SystemAPI.GetComponent<LocalToWorld>(target).Position);
                    if (SystemAPI.HasComponent<GridSource>(target)) value.TargetNameHash = SystemAPI.GetComponent<GridSource>(target).Blueprint;
                }
                if (SystemAPI.TryGetSingleton<InteractionTarget>(out var interaction) && interaction.Target != Entity.Null)
                {
                    value.Flags |= HudFlags.HasInteraction; value.InteractionKind = (byte)interaction.Kind;
                    value.InteractionDistanceM = interaction.DistanceM; value.InteractionInRange = interaction.InRange;
                    value.InteractionBodyKey = interaction.Body.LocalKey; value.InteractionGridPersistentId = interaction.GridPersistentId;
                }
                if (SystemAPI.TryGetSingleton<CurrentStarSystemInfo>(out var system)) value.StarSystemIdValue = system.Changing == 1 ? 0 : system.Id.Value;
            }
            if ((value.Flags & HudFlags.IsHulk) == 0)
            { lastLivePlayer = player; lastLiveMeters = value; }
            else if (lastLivePlayer == player)
            {
                value.Hull01 = lastLiveMeters.Hull01;
                value.ShieldHp = lastLiveMeters.ShieldHp; value.ShieldMax = lastLiveMeters.ShieldMax;
                value.ShieldStatus = lastLiveMeters.ShieldStatus; value.Energy = lastLiveMeters.Energy; value.EnergyMax = lastLiveMeters.EnergyMax;
            }
            blockHandle.Update(ref state); weaponHandle.Update(ref state); transformHandle.Update(ref state); entityHandle.Update(ref state);
            gridLookup.Update(ref state); allegianceLookup.Update(ref state); snapshotLookup.Update(ref state); radarLookup.Update(ref state);
            using (WeaponsMarker.Auto())
            {
                var groups = new NativeArray<HudWeaponGroupState>(3, Allocator.TempJob);
                for (byte i = 0; i < 3; i++) groups[i] = new HudWeaponGroupState { Group = i, Ready01 = 1 };
                state.Dependency = new HudWeaponGatherJob { Blocks = blockHandle, Weapons = weaponHandle, Player = player, Groups = groups }
                    .Schedule(weapons, state.Dependency);
                double now = UseTestClock ? TestElapsed : UnityEngine.Time.unscaledTimeAsDouble;
                if (now - lastRadar >= .1)
                {
                    using (RadarMarker.Auto())
                    {
                        lastRadar = now; value.RadarVersion = ++radarVersion;
                        var heap = new NativeArray<HudRadarCandidate>(64, Allocator.TempJob);
                        var count = new NativeReference<int>(Allocator.TempJob);
                        if (!SystemAPI.TryGetSingleton<FactionRelations>(out var relations)) relations = neutralRelations;
                        var gather = new HudRadarGatherJob { Entities = entityHandle, Transforms = transformHandle, Grids = gridLookup,
                            Allegiances = allegianceLookup, Relations = relations, PlayerPosition = position, PlayerFaction = faction,
                            Target = target, Range = 2000, Heap = heap, Count = count };
                        // TODO(HUD-009): resolve the current route gate into gather.Waypoint through the travel service bridge.
                        state.Dependency = gather.Schedule(grids, state.Dependency);
                        gather.Kind = 2; state.Dependency = gather.Schedule(bodies, state.Dependency);
                        gather.Kind = 3; state.Dependency = gather.Schedule(points, state.Dependency);
                        state.Dependency = gather.Schedule(bodyPoints, state.Dependency);
                        state.Dependency = new HudRadarSortJob { Heap = heap, Count = count, Blips = radarLookup, Snapshot = snapshot }.Schedule(state.Dependency);
                        state.Dependency = heap.Dispose(state.Dependency); state.Dependency = count.Dispose(state.Dependency);
                    }
                }
                state.Dependency = new PublishSnapshotJob { Groups = groups, Value = value, Snapshot = snapshot, Lookup = snapshotLookup }.Schedule(state.Dependency);
                state.Dependency = groups.Dispose(state.Dependency);
            }
        }

        [BurstCompile]
        struct PublishSnapshotJob : IJob
        {
            [ReadOnly] public NativeArray<HudWeaponGroupState> Groups;
            public HudSnapshot Value;
            public Entity Snapshot;
            public ComponentLookup<HudSnapshot> Lookup;
            public void Execute()
            {
                for (int i = 0; i < 3; i++) Value.WeaponGroups.Add(Groups[i]);
                Lookup[Snapshot] = Value;
            }
        }
    }
}
