using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Components;
using Game.Tests.Common;
using Game.Tests.EditMode.Voxel.Damage;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.DevTools
{
    /// <summary>DMG-017's two required checks invoke public Editor command bodies and the real resolver.</summary>
    public sealed class DamageCommandsTests : GameTestBase
    {
        [Serializable] sealed class Response
        {
            public bool ok = default;
            public string command = default, hash = default, error = default, persistentId = default;
            public uint tick = default, firstSequence = default;
            public bool queued = default;
            public int queuedEvents = default;
            public Counters lastTick = default;
            public GridCounters[] grids = default;
        }
        [Serializable] sealed class Counters
        { public uint tick = default; public int eventsIn = default, cellsDestroyed = default; }
        [Serializable] sealed class GridCounters
        { public string persistentId = default; public int cellsDestroyed = default; }

        static Response Command(string method, params object[] arguments)
        {
            // The test assembly has no DevTools reference. Invoke only these public harness entry points,
            // as HarnessCommandTests already discovers their assembly; no runtime game internals/ref changes.
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Game.DevTools.Editor");
            var type = assembly.GetType("Game.DevTools.Editor.Damage.DamageCommands", true);
            var entry = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null,
                arguments.Select(a => a.GetType()).ToArray(), null);
            Assert.IsNotNull(entry, method);
            string json = (string)entry.Invoke(null, arguments);
            StringAssert.StartsWith("{", json); StringAssert.EndsWith("}", json);
            Assert.IsFalse(json.Contains('\n') || json.Contains('\r'), "Each result is one JSON line.");
            var response = JsonUtility.FromJson<Response>(json); // Harness output, not save serialization.
            Assert.IsNotNull(response); Assert.IsNotEmpty(response.command);
            return response;
        }

        static TestGridFactory Fixture()
        {
            if (GameLog.GetMinimumLevel(LogCategories.Content) <= LogLevel.Info)
                LogAssert.Expect(LogType.Log, new Regex(@"^\[Content\] Content:"));
            return new TestGridFactory();
        }

        static void DrainEmptyExplosions(World world, SystemHandle owner)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(DamageStreams));
            var streams = query.GetSingleton<DamageStreams>();
            ref var state = ref world.Unmanaged.ResolveSystemStateRef(owner);
            // This resolver-only fixture has no ExplosionSystem. Consume its empty publication before it becomes stale.
            streams.Explosions.BeginRead(ref state); streams.Explosions.EndRead(state.Dependency);
        }

        [Test]
        public void Hash_PrintsJson()
        {
            using var fixture = Fixture();
            var root = fixture.Spawn(fixture.Box(new int3(-1), new int3(1)));
            fixture.EM.CreateSingleton(new SimClock { Tick = 19 });
            fixture.World.CreateSystem<DamageStreamsSystem>();
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var result = Command("Hash", fixture.World);
                Assert.IsTrue(result.ok, result.error); Assert.AreEqual("gh_dmg_hash", result.command);
                Assert.AreEqual("0x" + CellStateHasher.ComputeWorldHash(fixture.EM).ToString("X16", CultureInfo.InvariantCulture), result.hash);
                Assert.AreEqual(19u, result.tick);
                Assert.AreEqual(result.hash, Command("Hash", fixture.World).hash, "Read-only command.");
                var stats = Command("Stats", fixture.World);
                Assert.IsTrue(stats.ok, stats.error); Assert.AreEqual("gh_dmg_stats", stats.command);
                Assert.AreEqual(1, stats.grids.Length);
                Assert.AreEqual(fixture.EM.GetComponentData<PersistentId>(root).Value.ToString(CultureInfo.InvariantCulture), stats.grids[0].persistentId);
                Assert.AreEqual(0, stats.grids[0].cellsDestroyed);
                var notPlaying = Command("Hash");
                Assert.IsFalse(notPlaying.ok); Assert.AreEqual("not-playing", notPlaying.error);
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }

        [Test]
        public void Hit_RemovesCells()
        {
            using var fixture = Fixture();
            var root = fixture.Spawn(fixture.Box(new int3(-1, 0, 0), new int3(1, 0, 0)));
            var overlapping = fixture.Spawn(fixture.Box(new int3(-1, 0, 0), new int3(1, 0, 0)));
            var pose = LocalTransform.FromPositionRotation(new float3(12, -3, 8), quaternion.RotateY(math.PI / 2));
            fixture.EM.SetComponentData(root, pose); fixture.EM.SetComponentData(overlapping, pose);
            var clock = fixture.EM.CreateSingleton(new SimClock());
            var owner = fixture.World.CreateSystem<DamageStreamsSystem>();
            var resolver = fixture.World.CreateSystem<DamageResolveSystem>();
            ulong before = CellStateHasher.ComputeWorldHash(fixture.EM);
            float3 worldPoint = pose.TransformPoint(new float3(.25f));
            var result = Command("Hit", fixture.World, worldPoint.x, worldPoint.y, worldPoint.z, 1000f, 0f);
            Assert.IsTrue(result.ok, result.error); Assert.AreEqual("gh_dmg_hit", result.command);
            Assert.IsTrue(result.queued); Assert.AreEqual(1, result.queuedEvents);
            Assert.AreEqual(fixture.EM.GetComponentData<PersistentId>(root).Value.ToString(CultureInfo.InvariantCulture), result.persistentId);
            Assert.AreEqual(before, CellStateHasher.ComputeWorldHash(fixture.EM), "CLI only queues; it never writes cells.");
            var invalid = Command("Hit", fixture.World, worldPoint.x, worldPoint.y, worldPoint.z, float.NaN, 0f);
            Assert.IsFalse(invalid.ok); Assert.AreEqual("invalid-argument", invalid.error);
            fixture.EM.SetComponentData(clock, new SimClock { Tick = 1 });
            owner.Update(fixture.World.Unmanaged); resolver.Update(fixture.World.Unmanaged);
            fixture.EM.CompleteAllTrackedJobs();
            Assert.AreNotEqual(before, CellStateHasher.ComputeWorldHash(fixture.EM));
            using (var writer = fixture.Writer(root))
            {
                Assert.IsFalse(writer.Access.TryGetCell(default, out _, out _, out _));
                Assert.IsTrue(writer.Access.TryGetCell(new int3(-1, 0, 0), out _, out _, out _));
                Assert.IsTrue(writer.Access.TryGetCell(new int3(1, 0, 0), out _, out _, out _));
            }
            Assert.AreEqual(1, fixture.EM.GetComponentData<GridDamageStats>(root).CellsDestroyed);
            Assert.AreEqual(0, fixture.EM.GetComponentData<GridDamageStats>(overlapping).CellsDestroyed);
            DrainEmptyExplosions(fixture.World, owner);
            fixture.EM.SetComponentData(clock, new SimClock { Tick = 2 }); owner.Update(fixture.World.Unmanaged);
            var stats = Command("Stats", fixture.World);
            Assert.IsTrue(stats.ok, stats.error);
            Assert.AreEqual(1u, stats.lastTick.tick); Assert.AreEqual(1, stats.lastTick.eventsIn); Assert.AreEqual(1, stats.lastTick.cellsDestroyed);
            Assert.AreEqual(1, stats.grids.Single(g => g.persistentId == result.persistentId).cellsDestroyed);

            var coreRoot = fixture.Spawn(fixture.CoreAndReactor());
            string coreId = fixture.EM.GetComponentData<PersistentId>(coreRoot).Value.ToString(CultureInfo.InvariantCulture);
            ulong coreBefore = CellStateHasher.ComputeWorldHash(fixture.EM);
            var killed = Command("KillCore", fixture.World, coreId);
            Assert.IsTrue(killed.ok, killed.error); Assert.AreEqual("gh_dmg_kill_core", killed.command);
            Assert.AreEqual(coreId, killed.persistentId); Assert.AreEqual(7, killed.queuedEvents, "Every remaining cell of the seven-cell core is targeted.");
            Assert.Greater(killed.firstSequence, result.firstSequence);
            Assert.AreEqual(coreBefore, CellStateHasher.ComputeWorldHash(fixture.EM));
            fixture.EM.SetComponentData(clock, new SimClock { Tick = 3 });
            owner.Update(fixture.World.Unmanaged); resolver.Update(fixture.World.Unmanaged); fixture.EM.CompleteAllTrackedJobs();
            Assert.AreEqual(-1, fixture.EM.GetComponentData<VoxelGrid>(coreRoot).AnchorInstance);
            Assert.AreEqual(1, fixture.EM.GetComponentData<GridDamageStats>(coreRoot).InstancesDestroyed);
            using (var writer = fixture.Writer(coreRoot))
                foreach (var cell in new[] { new int3(0), new int3(0,1,0), new int3(0,2,0), new int3(1,1,0),
                    new int3(-1,1,0), new int3(0,1,1), new int3(0,1,-1) })
                    Assert.IsFalse(writer.Access.TryGetCell(cell, out _, out _, out _), cell.ToString());
        }
    }
}
