using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation.Commands;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class GalaxyServiceTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        sealed class Session : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Fixture = new(2, 1, 42, false);
            internal readonly World World = new("Galaxy service fixture");
            internal readonly GalaxyState State = new();
            internal readonly GalaxyService Service;
            internal Session()
            {
                var catalog = ContentCatalog.Build(Fixture.Definitions, new ValidationReport("Galaxy service"));
                Service = new GalaxyService(catalog, State, World, new GalaxyFrameTicker()); Service.NewGame(42);
            }
            public void Dispose() { Service.Dispose(); State.Dispose(); World.Dispose(); Fixture.Dispose(); }
        }
        [Test] public void Restore_SameHash()
        {
            using var session = new Session(); var service = session.Service;
            ulong hash = service.Galaxy.Hash;
            Assert.IsTrue(session.State.IsKnown(service.Galaxy.StartStarSystem));
            Assert.IsTrue(session.State.IsVisited(service.Galaxy.StartStarSystem));
            service.OnSessionRestored(); Assert.AreEqual(hash, service.Galaxy.Hash); Assert.AreEqual(2, service.GenerationCount);
            var clock = session.World.EntityManager.CreateEntity(typeof(SimClock));
            session.World.EntityManager.SetComponentData(clock, new SimClock { UniversalTime = 123 });
            var start = service.GetStartLocation();
            Assert.IsTrue(service.TryResolveBody(service.Definition.StartStarSystem.Id, service.Definition.StartStarSystem.StartBodyKey, out var bodyId));
            var content = service.GetContent(bodyId.System); Assert.IsTrue(content.TryFind(bodyId.LocalKey, out int index));
            var body = content.Bodies[index];
            var expected = OrbitMath.Position(body, 123) + (double3)math.mul(Planar.YawRotation(body.YawRad), (float3)service.Definition.StartStarSystem.StartOffsetM);
            Assert.AreEqual(expected, start.PositionM); Assert.AreEqual(math.radians(service.Definition.StartStarSystem.StartYawDegrees), start.YawRad);
            Assert.IsFalse(service.CapturePlayerLocation());
            var player = session.World.EntityManager.CreateEntity(typeof(PlayerShip), typeof(UniversePosition), typeof(LocalTransform));
            session.World.EntityManager.SetComponentData(player, new UniversePosition { Value = new double3(1e10, 0, -5) });
            session.World.EntityManager.SetComponentData(player, LocalTransform.FromPositionRotation(float3.zero, Planar.YawRotation(.7f)));
            Assert.IsTrue(service.CapturePlayerLocation()); Assert.IsTrue(session.State.HasPlayerLocation);
            Assert.AreEqual(new double3(1e10, 0, -5), session.State.PlayerLocation.PositionM);
            Assert.That(session.State.PlayerLocation.YawRad, Is.EqualTo(.7f).Within(.00001f));
        }
        [Test] public void Content_GeneratedOnce()
        {
            using var session = new Session(); var service = session.Service; var id = service.Galaxy.StartStarSystem;
            var first = service.GetContent(id); Assert.AreSame(first, service.GetContent(id));
            Assert.AreEqual(1, service.ContentGenerationCount(id));
            service.ReleaseContent(id); Assert.AreNotSame(first, service.GetContent(id)); Assert.AreEqual(2, service.ContentGenerationCount(id));
        }
        [Test] public void Prefetch_EqualsSync()
        {
            using var session = new Session(); var service = session.Service; var id = service.Galaxy.StartStarSystem;
            var sync = service.GetContent(id); var bodies = sync.Bodies.AsArray().ToArray(); ulong hash = sync.GeneratedHash;
            service.ReleaseContent(id); var handle = service.PrefetchContent(id);
            Assert.AreEqual(handle, service.PrefetchContent(id));
            var prefetched = service.GetContent(id); CollectionAssert.AreEqual(bodies, prefetched.Bodies.AsArray().ToArray()); Assert.AreEqual(hash, prefetched.GeneratedHash);
            service.ReleaseContent(id); service.PrefetchContent(id); service.ReleaseContent(id);
            Assert.AreEqual(2, service.ContentGenerationCount(id), "Released pending work does not become cached content.");
        }
        sealed class Sink : IGalaxyLiveDeltaSink
        {
            internal readonly List<BodyId> Changes = new();
            public void OnBodyDeltaChanged(BodyId body) => Changes.Add(body);
            public void FlushStructures() { }
        }
        [Test] public void DeltaWrite_PatchesCacheAndNotifies()
        {
            using var session = new Session(); var service = session.Service; var sink = new Sink(); service.SetLiveDeltaSink(sink);
            var content = service.GetContent(service.Galaxy.StartStarSystem); var body = content.Bodies[0]; ulong hash = content.GeneratedHash;
            service.ReplaceBody(body.Id, session.Fixture.Station.Id); Assert.AreEqual(1, sink.Changes.Count);
            Assert.AreEqual(service.Galaxy.BodyTemplateIndexOf(session.Fixture.Station), content.Bodies[0].TemplateIndex);
            Assert.IsTrue(service.MarkBodyInteracted(body.Id)); Assert.IsFalse(service.MarkBodyInteracted(body.Id));
            Assert.IsTrue(service.MarkBodyDestroyed(body.Id)); Assert.IsFalse(service.MarkBodyDestroyed(body.Id)); Assert.IsTrue(service.IsBodyDestroyed(body.Id));
            Assert.AreEqual(3, sink.Changes.Count);
            var spawned = service.AddStorySpawn(body.Id.System, session.Fixture.Station.Id, default, new double3(10, 0, 20), .3f);
            Assert.IsTrue(content.TryFind(spawned.LocalKey, out _)); Assert.AreEqual(4, sink.Changes.Count);
            Assert.IsTrue(service.RemoveStorySpawn(spawned)); Assert.IsFalse(service.RemoveStorySpawn(spawned));
            Assert.IsFalse(content.TryFind(spawned.LocalKey, out _)); Assert.AreEqual(5, sink.Changes.Count);
            Assert.AreSame(content, service.GetContent(body.Id.System)); Assert.AreEqual(hash, content.GeneratedHash);
            var before = content.Bodies.AsArray().ToArray(); service.ReleaseContent(body.Id.System);
            CollectionAssert.AreEqual(before, service.GetContent(body.Id.System).Bodies.AsArray().ToArray());
        }
        [Test] public void Names_Format()
        {
            using var session = new Session(); var service = session.Service; var definition = service.Definition.StartStarSystem;
            Assert.AreEqual(definition.DisplayName, service.GetStarSystemName(service.Galaxy.StartStarSystem));
            Assert.IsTrue(service.TryResolveBody(definition.Id, "planet", out var planet));
            Assert.AreEqual(definition.DisplayName + " I", service.GetBodyName(planet));
            Assert.IsTrue(service.TryResolveBody(definition.Id, "moon_b", out var moon));
            Assert.AreEqual(definition.DisplayName + " I b", service.GetBodyName(moon));
            Assert.AreEqual(definition.IdString + "/moon_b", service.GetLocationKey(moon));
            foreach (var system in service.Galaxy.StarSystems)
                if (system.AuthoredIndex == 0)
                    Assert.AreEqual(session.Fixture.Template.NamePrefix + " " + (char)('A' + system.Seed % 26) + "-" + system.Id.Value.ToString("D4"), service.GetStarSystemName(system.Id));
            var gate = service.GetContent(planet.System).Bodies.AsArray().ToArray().First(b => b.Id.Role == BodyRole.LaneGate);
            Assert.AreEqual("Gate to " + service.GetStarSystemName(GalaxyDef.Neighbour(service.Galaxy.Lanes[gate.LaneIndex], planet.System)), service.GetBodyName(gate.Id));
        }
        sealed class Early : IGalaxyFrameTick
        {
            readonly List<string> calls; internal Early(List<string> calls) { this.calls = calls; }
            public int TickOrder => 0; public void Tick(in GalaxyFrameContext context) => calls.Add("early");
        }
        sealed class AThrow : IGalaxyFrameTick
        {
            readonly List<string> calls; internal AThrow(List<string> calls) { this.calls = calls; }
            public int TickOrder => 100;
            public void Tick(in GalaxyFrameContext context) { calls.Add("a"); throw new InvalidOperationException("expected ticker failure"); }
        }
        sealed class BLast : IGalaxyFrameTick
        {
            readonly List<string> calls; internal BLast(List<string> calls) { this.calls = calls; }
            public int TickOrder => 100; public void Tick(in GalaxyFrameContext context) => calls.Add("b");
        }
        [Test] public void Ticker_OrderAndIsolation()
        {
            var calls = new List<string>(); var ticker = new GalaxyFrameTicker(); var a = new AThrow(calls); var b = new BLast(calls);
            ticker.Register(b); ticker.Register(a); ticker.Register(new Early(calls)); ticker.Register(b);
            LogAssert.Expect(LogType.Error, new Regex("tick failed:.*expected ticker failure"));
            ticker.Tick(default); CollectionAssert.AreEqual(new[] { "early", "a", "b" }, calls);
            calls.Clear(); ticker.Unregister(a); ticker.Tick(default); CollectionAssert.AreEqual(new[] { "early", "b" }, calls);
        }
    }
}
