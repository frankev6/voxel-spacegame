using System.Collections.Generic;
using System.Reflection;
using Game.App.Spawning;
using Game.Core;
using Game.Core.Components;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>SYS-010: test-only spawn mapping/tagging contract; FND-012 owns the real request, profile and hook implementation.</summary>
    public sealed class ShipSpawnerContractTests
    {
        [Test]
        public void Spawn_MapsArgs()
        {
            using var world = new World("SYS-010 argument mapping");
            var fake = new FakeSpawner(world.EntityManager);
            IShipSpawner spawner = fake;
            var args = new GalaxyShipSpawnArgs
            {
                Blueprint = new ContentId("ship_contract_fixture"), Position = new float3(12, 3, -45),
                Rotation = quaternion.EulerXYZ(.2f, .7f, -.4f), LinearVelocity = new float3(2, 0, -8),
                Allegiance = new Allegiance { Faction = 3, Team = 7 }, WithAi = true,
                AiProfile = new ContentId("aiprof_contract_fixture"), StarSystem = new StarSystemId(17), Encounter = new ContentHash(99)
            };
            var id = spawner.Spawn(in args);
            Assert.That(id.Value, Is.Not.Zero);
            AssertMapping(args, fake.LastFoundationArgs, fake.LastRequesterAllegiance);
            Assert.That(fake.LastGalaxyArgs.StarSystem, Is.EqualTo(args.StarSystem));
            Assert.That(fake.LastGalaxyArgs.Encounter, Is.EqualTo(args.Encounter), "Encounter identity remains caller-owned (SPEC-33).");
            Assert.That(spawner.TryGetRoot(id.Value, out var root), Is.True);
            Assert.That(world.EntityManager.GetComponentData<PersistentId>(root).Value, Is.EqualTo(id.Value));
            Assert.That(world.EntityManager.GetComponentData<Allegiance>(root), Is.EqualTo(args.Allegiance));

            // Optional AI profile and no-encounter values pass through unchanged; IDs remain independently allocated.
            args.WithAi = false; args.AiProfile = ContentId.None; args.Encounter = default;
            args.Position = new float3(-9, 0, 28); args.Rotation = quaternion.identity; args.LinearVelocity = float3.zero;
            var second = spawner.Spawn(in args);
            Assert.That(second.Value, Is.Not.EqualTo(id.Value));
            AssertMapping(args, fake.LastFoundationArgs, fake.LastRequesterAllegiance);
            Assert.That(fake.LastGalaxyArgs.Encounter.Value, Is.Zero);
            Assert.That(spawner.Despawn(id.Value), Is.True);
            Assert.That(spawner.TryGetRoot(id.Value, out var missing), Is.False); Assert.That(missing, Is.EqualTo(Entity.Null));
            Assert.That(spawner.Despawn(id.Value), Is.False);
            Assert.That(spawner.TryGetRoot(second.Value, out _), Is.True);
        }

        [Test]
        public void StarSystemMember_Set()
        {
            using var world = new World("SYS-010 star system membership");
            IShipSpawner spawner = new FakeSpawner(world.EntityManager);
            var args = new GalaxyShipSpawnArgs
            {
                Blueprint = new ContentId("ship_contract_fixture"), Position = new float3(23, 0, -16), Rotation = quaternion.identity,
                Allegiance = Allegiance.None, StarSystem = new StarSystemId(31)
            };
            var first = spawner.Spawn(in args);
            AssertMember(spawner, world.EntityManager, first, args.StarSystem);
            args.StarSystem = new StarSystemId(32);
            var second = spawner.Spawn(in args);
            AssertMember(spawner, world.EntityManager, second, args.StarSystem);
            AssertMember(spawner, world.EntityManager, first, new StarSystemId(31));
            Assert.That(spawner.Despawn(first.Value), Is.True);
            Assert.That(spawner.TryGetRoot(first.Value, out _), Is.False);
            AssertMember(spawner, world.EntityManager, second, new StarSystemId(32));
        }

        static void AssertMapping(in GalaxyShipSpawnArgs source, in ShipSpawnArgs mapped, in Allegiance requesterAllegiance)
        {
            Assert.That(mapped.Blueprint, Is.EqualTo(source.Blueprint));
            Assert.That(mapped.Structure, Is.Null);
            Assert.That(mapped.Position, Is.EqualTo(source.Position));
            Assert.That(mapped.Rotation, Is.EqualTo(source.Rotation), "Yaw-only conversion belongs to FND-012's later GridSpawnParams construction.");
            Assert.That(mapped.LinearVelocity, Is.EqualTo(source.LinearVelocity));
            Assert.That(mapped.WithAi, Is.EqualTo(source.WithAi)); Assert.That(mapped.AiProfile, Is.EqualTo(source.AiProfile));
            Assert.That(mapped.IsPlayer, Is.False); Assert.That(mapped.PersistentId, Is.Zero);
            Assert.That(requesterAllegiance, Is.EqualTo(source.Allegiance));
            // FND-001 deliberately deferred this field. Keep this fixture compatible when FND-012 adds its canonical field.
            var field = typeof(ShipSpawnArgs).GetField("Allegiance", BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                Assert.That(field.FieldType, Is.EqualTo(typeof(Allegiance)));
                Assert.That(field.GetValue(mapped), Is.EqualTo(source.Allegiance));
            }
        }

        // These interface/root assertions also apply to the real spawner's FND-012 GalaxyArgs_UsesStarSystem test.
        static void AssertMember(IShipSpawner spawner, EntityManager em, PersistentIdValue id, StarSystemId expected)
        {
            Assert.That(spawner.TryGetRoot(id.Value, out var root), Is.True);
            Assert.That(em.Exists(root), Is.True);
            Assert.That(em.GetSharedComponent<StarSystemMember>(root).Id, Is.EqualTo(expected));
            Assert.That(em.GetComponentData<PersistentId>(root).Value, Is.EqualTo(id.Value));
        }

        /// <summary>Records the SPEC-01 §5.7 adapter; simple test entities expose only identity, allegiance and membership.</summary>
        sealed class FakeSpawner : IShipSpawner
        {
            readonly EntityManager em;
            readonly Dictionary<ulong, Entity> roots = new();
            ulong nextId = 1;
            public GalaxyShipSpawnArgs LastGalaxyArgs;
            public ShipSpawnArgs LastFoundationArgs;
            public Allegiance LastRequesterAllegiance;
            public FakeSpawner(EntityManager em) { this.em = em; }

            public PersistentIdValue Spawn(in GalaxyShipSpawnArgs args)
            {
                LastGalaxyArgs = args;
                LastFoundationArgs = new ShipSpawnArgs
                {
                    Blueprint = args.Blueprint, Structure = null, Position = args.Position, Rotation = args.Rotation,
                    LinearVelocity = args.LinearVelocity, WithAi = args.WithAi, AiProfile = args.AiProfile,
                    IsPlayer = false, PersistentId = 0
                };
                LastRequesterAllegiance = args.Allegiance;
                var field = typeof(ShipSpawnArgs).GetField("Allegiance", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    object boxed = LastFoundationArgs; field.SetValue(boxed, args.Allegiance); LastFoundationArgs = (ShipSpawnArgs)boxed;
                }
                ulong id = nextId++;
                var root = em.CreateEntity();
                em.AddComponentData(root, new PersistentId(id));
                em.AddComponentData(root, args.Allegiance);
                em.AddSharedComponent(root, new StarSystemMember { Id = args.StarSystem });
                roots.Add(id, root);
                return new PersistentIdValue(id);
            }

            public bool TryGetRoot(ulong persistentId, out Entity root)
            {
                if (roots.TryGetValue(persistentId, out root) && em.Exists(root)) return true;
                roots.Remove(persistentId); root = Entity.Null; return false;
            }

            public bool Despawn(ulong persistentId)
            {
                if (!TryGetRoot(persistentId, out var root)) return false;
                em.DestroyEntity(root); roots.Remove(persistentId); return true;
            }
        }
    }
}
