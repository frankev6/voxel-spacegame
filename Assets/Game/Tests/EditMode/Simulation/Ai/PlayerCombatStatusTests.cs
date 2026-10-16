using System;
using System.Collections.Generic;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>SPEC-23 AI-008: the three required production-system fixtures, including native guard and boot lifetime.</summary>
    public sealed class PlayerCombatStatusTests : GameTestBase
    {
        [Test] public void CountsEngagingHostiles()
        {
            using var f = new Fixture();
            var player = f.Player(0);
            var proxies = new List<SpatialProxy> { Proxy(player, float3.zero) };
            var blocked = f.Ai(player); f.Em.SetComponentData(blocked, new AiBrain { State = AiState.Engage, FireBlocked = 1 });
            proxies.Add(Proxy(blocked, new float3(50, 0, 0)));
            var boundary = f.Ai(player); proxies.Add(Proxy(boundary, new float3(0, 0, 100), ProxyKind.Station));
            var dormant = f.Ai(player); f.Em.AddComponent<Dormant>(dormant); proxies.Add(Proxy(dormant, new float3(0, 0, 10)));
            proxies.Add(Proxy(f.Ai(player), new float3(100.01f, 0, 0), radius: 5000)); // Sphere overlap is not centre distance.
            proxies.Add(Proxy(f.Ai(player), new float3(0, 100.01f, 0))); // Bubble is three-dimensional.
            proxies.Add(Proxy(f.Ai(player, faction: 0), float3.zero));
            proxies.Add(Proxy(f.Ai(player, faction: 2), float3.zero));
            proxies.Add(Proxy(f.Ai(player, state: AiState.Flee), float3.zero));
            proxies.Add(Proxy(f.Ai(player, state: AiState.Hold), float3.zero));
            proxies.Add(Proxy(f.Ai(blocked), float3.zero));
            var hulk = f.Ai(player); f.Em.AddComponent<Hulk>(hulk); proxies.Add(Proxy(hulk, float3.zero)); // Stale Ship proxy.
            proxies.Add(Proxy(f.Ai(player), float3.zero, ProxyKind.Hulk));
            proxies.Add(Proxy(f.Ai(player), float3.zero, ProxyKind.Asteroid));
            proxies.Add(Proxy(f.Ai(player), float3.zero, ProxyKind.Celestial));
            proxies.Add(Proxy(f.Ai(player), new float3(float.NaN, 0, 0)));
            var spawning = f.Ai(player); f.Em.AddComponent<GridSpawning>(spawning); proxies.Add(Proxy(spawning, float3.zero));
            var incomplete = f.Ai(player); f.Em.RemoveComponent<ShipTarget>(incomplete); proxies.Add(Proxy(incomplete, float3.zero));
            var noBrain = f.Ai(player); f.Em.RemoveComponent<AiBrain>(noBrain); proxies.Add(Proxy(noBrain, float3.zero));
            var noAllegiance = f.Ai(player); f.Em.RemoveComponent<Allegiance>(noAllegiance); proxies.Add(Proxy(noAllegiance, float3.zero));
            var stale = f.Ai(player); proxies.Add(Proxy(stale, float3.zero)); f.Em.DestroyEntity(stale);
            // More than 127 matches proves combat counting cannot be truncated by a FixedList sphere result.
            for (int i = 0; i < 140; i++) proxies.Add(Proxy(f.Ai(player), new float3(1, 0, 1)));
            f.Publish(proxies.ToArray()); f.Update(); Assert.AreEqual(143, f.Status.HostilesEngaging);
            // Current allegiance overrides a previous-tick hostile proxy; brain/target remain unchanged.
            f.Em.SetComponentData(blocked, Allegiance.Player); f.Update(); Assert.AreEqual(142, f.Status.HostilesEngaging);
            f.Em.SetComponentEnabled<GridSpawning>(spawning, false); f.Update(); Assert.AreEqual(143, f.Status.HostilesEngaging);
        }

        [Test] public void LastDamagedTick_Follows()
        {
            using var f = new Fixture(); var player = f.Player(22);
            f.Publish(Proxy(player, float3.zero)); f.Update(); Assert.AreEqual(22u, f.Status.LastDamagedTick);
            f.Em.SetComponentData(player, new GridDamageStats { LastDamageTick = 56 }); f.Update(); Assert.AreEqual(56u, f.Status.LastDamagedTick);
            f.Em.SetComponentData(player, default(GridDamageStats)); f.Update(); Assert.AreEqual(0u, f.Status.LastDamagedTick);
            f.Em.RemoveComponent<GridDamageStats>(player); f.Update(); Assert.AreEqual(0u, f.Status.LastDamagedTick);
            f.Em.AddComponentData(player, new GridDamageStats { LastDamageTick = 100 });
            f.Publish(); f.Update(); Assert.AreEqual(100u, f.Status.LastDamagedTick, "Damage tracking does not require an available player proxy.");
        }

        [Test] public void NoPlayer_Zero()
        {
            using var f = new Fixture(); Assert.AreEqual(0, f.Status.HostilesEngaging); Assert.AreEqual(0u, f.Status.LastDamagedTick);
            var owner = f.StatusEntity; f.Publish(); f.Update(); Assert.AreEqual(0, f.Status.HostilesEngaging);
            var player = f.Player(77); var enemy = f.Ai(player);
            f.Publish(Proxy(player, float3.zero), Proxy(enemy, new float3(10, 0, 0))); f.Update();
            Assert.AreEqual(1, f.Status.HostilesEngaging); Assert.AreEqual(77u, f.Status.LastDamagedTick);
            f.Em.RemoveComponent<PlayerShip>(player); f.Update(); Assert.AreEqual(0, f.Status.HostilesEngaging); Assert.AreEqual(77u, f.Status.LastDamagedTick);
            f.Em.AddComponent<PlayerShip>(player); var duplicate = f.Player(999); f.Update();
            Assert.AreEqual(0, f.Status.HostilesEngaging); Assert.AreEqual(77u, f.Status.LastDamagedTick, "Ambiguous player tags cannot pick by chunk order.");
            f.Em.DestroyEntity(duplicate); f.Em.DestroyEntity(player); f.Update();
            Assert.AreEqual(0, f.Status.HostilesEngaging); Assert.AreEqual(77u, f.Status.LastDamagedTick); Assert.IsTrue(f.Em.Exists(owner));
            using (var query = f.Em.CreateEntityQuery(typeof(PlayerCombatStatus))) Assert.AreEqual(1, query.CalculateEntityCount());
            f.StopStatus(); Assert.IsFalse(f.Em.Exists(owner), "OnDestroy destroys only the boot-owned status entity.");
        }

        static SpatialProxy Proxy(Entity root, float3 centre, ProxyKind kind = ProxyKind.Ship, float radius = 1)
            => new SpatialProxy { Entity = root, Center = centre, Radius = radius, HullRadius = radius, Kind = kind };

        struct PublishProxies : IJob
        {
            [ReadOnly] public NativeArray<SpatialProxy> Source;
            public NativeList<SpatialProxy> Proxies;
            public void Execute()
            { Proxies.Clear(); for (int i = 0; i < Source.Length; i++) Proxies.Add(Source[i]); }
        }

        sealed class Fixture : IDisposable
        {
            readonly EcsTestWorld world;
            readonly SpatialHash hash;
            FactionRelations relations;
            bool statusAlive = true;
            public EntityManager Em => world.EntityManager;
            public readonly Entity StatusEntity;
            public PlayerCombatStatus Status
            {
                get { Em.CompleteDependencyBeforeRO<PlayerCombatStatus>(); return Em.GetComponentData<PlayerCombatStatus>(StatusEntity); }
            }
            public Fixture()
            {
                world = EcsTestWorld.Create(typeof(PlayerCombatStatusSystem), typeof(PlayerCombatStatusSyncSystem));
                using (var query = Em.CreateEntityQuery(typeof(PlayerCombatStatus)))
                {
                    Assert.AreEqual(1, query.CalculateEntityCount(), "Status exists at system creation, before gameplay/config/player.");
                    StatusEntity = query.GetSingletonEntity();
                }
                hash = SpatialHash.Create(128); relations = FactionRelations.CreateDefault(Allocator.Persistent);
                world.AddSingleton(hash); world.AddSingleton(relations); world.AddSingleton(new AiConfig { CombatBubbleRadius = 100 }); world.AddGameplayActive();
            }
            public Entity Player(uint damageTick)
            {
                // ADR-0117: the player is a pilot, counted into its PilotCombatStatus and synced into the singleton.
                var entity = Em.CreateEntity(typeof(PlayerShip), typeof(Pilot), typeof(PilotCombatStatus), typeof(Allegiance), typeof(GridDamageStats));
                Em.SetComponentData(entity, Allegiance.Player); Em.SetComponentData(entity, new GridDamageStats { LastDamageTick = damageTick }); return entity;
            }
            public Entity Ai(Entity target, byte faction = 1, AiState state = AiState.Engage)
            {
                var entity = Em.CreateEntity(typeof(AiBrain), typeof(ShipTarget), typeof(Allegiance));
                Em.SetComponentData(entity, new AiBrain { State = state }); Em.SetComponentData(entity, new ShipTarget { Value = target });
                Em.SetComponentData(entity, new Allegiance { Faction = faction }); return entity;
            }
            public void Publish(params SpatialProxy[] proxies)
            {
                Array.Sort(proxies, (a, b) => { int order = a.Entity.Index.CompareTo(b.Entity.Index); return order != 0 ? order : a.Entity.Version.CompareTo(b.Entity.Version); });
                var source = new NativeArray<SpatialProxy>(proxies, Allocator.TempJob);
                var published = new PublishProxies { Source = source, Proxies = hash.Proxies }.Schedule(hash.Access.Acquire(default));
                published = source.Dispose(published); hash.Access.Release(published); // Deliberately not an ECS component writer dependency.
            }
            public void Update() { world.UpdateSystem<PlayerCombatStatusSystem>(); world.UpdateSystem<PlayerCombatStatusSyncSystem>(); }
            public void StopStatus()
            {
                if (!statusAlive) return;
                world.World.DestroySystem(world.World.Unmanaged.GetExistingUnmanagedSystem<PlayerCombatStatusSystem>()); statusAlive = false;
            }
            public void Dispose()
            { StopStatus(); hash.Dispose(); relations.Dispose(); world.Dispose(); }
        }
    }
}
