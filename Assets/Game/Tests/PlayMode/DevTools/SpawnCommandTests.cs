using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.Core.Components;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.PlayMode.DevTools
{
    public sealed class SpawnCommandTests
    {
        [Test] public void CountSpacing_DistinctIds()
        {
            using var f = new WorldCommandFixture();
            var result = f.Command("spawn blueprint=" + f.Blueprint.IdString + " count=3 spacing=60");
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
            Assert.IsTrue(f.Host.TryGetOperation(result.OperationId, out var operation));
            Assert.AreEqual(DevOperationStatus.Running, operation.Status);
            f.Host.Update(); Assert.AreEqual(DevOperationStatus.Running, operation.Status, "Requests are not converted grid roots yet.");
            for (int i = 0; i < 3 && operation.Status == DevOperationStatus.Running; i++) { f.Test.StepTicks(1); f.Host.Update(); }
            Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
            using var query = f.Em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<PersistentId>());
            using var roots = query.ToEntityArray(Allocator.Temp); var positions = new List<float3>(); var ids = new HashSet<ulong>();
            foreach (var root in roots)
            {
                Assert.IsTrue(ids.Add(f.Em.GetComponentData<PersistentId>(root).Value));
                if (root == f.Player) continue;
                positions.Add(f.Em.GetComponentData<LocalTransform>(root).Position);
                Assert.IsTrue(f.Em.HasComponent<AiBrain>(root));
                Assert.AreEqual(new Allegiance { Faction = 255, Team = 1 }, f.Em.GetComponentData<Allegiance>(root));
            }
            Assert.AreEqual(4, ids.Count); Assert.AreEqual(3, positions.Count); positions.Sort((a, b) => a.x.CompareTo(b.x));
            for (int i = 0; i < 3; i++) Assert.AreEqual(new float3(i * 60, 0, 80), positions[i]);
            result = f.Command("spawn blueprint=" + f.Blueprint.IdString + " x=5 z=6 ai=false team=7 face=player");
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text); f.Test.StepTicks(1); f.Host.Update();
            Assert.IsTrue(f.Host.TryGetOperation(result.OperationId, out operation)); Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status);
            using var updated = query.ToEntityArray(Allocator.Temp); Entity unpiloted = Entity.Null;
            foreach (var root in updated)
                if (root != f.Player && !f.Em.HasComponent<AiBrain>(root)) { Assert.AreEqual(Entity.Null, unpiloted); unpiloted = root; }
            Assert.AreNotEqual(Entity.Null, unpiloted);
            var placed = f.Em.GetComponentData<LocalTransform>(unpiloted);
            Assert.AreEqual(new float3(5, 0, 6), placed.Position);
            Assert.AreEqual(new Allegiance { Faction = 255, Team = 7 }, f.Em.GetComponentData<Allegiance>(unpiloted));
            Assert.Greater(math.dot(math.mul(placed.Rotation, new float3(0, 0, 1)), math.normalize(-placed.Position)), .999f);
            var nearest = f.Command("grid nearest=true"); Assert.AreEqual(DevResultKind.Ok, nearest.Kind, nearest.Text);
            StringAssert.Contains("PersistentId=" + f.Em.GetComponentData<PersistentId>(unpiloted).Value, nearest.Text);
            Assert.AreEqual(DevResultKind.Failed, f.Command("spawn blueprint=unknown").Kind);
            Assert.AreEqual(DevResultKind.Failed, f.Command("spawn blueprint=" + f.Blueprint.IdString + " x=1").Kind);
            f.State.CurrentStarSystem = default;
            var request = new Game.App.Spawning.ShipSpawnArgs { Blueprint = f.Blueprint.Id, Rotation = quaternion.identity };
            Assert.Throws<System.InvalidOperationException>(() => f.Spawner.Spawn(request));
            // Arrange the real session's mode without a scene transition; keep this fixture one cell/direct ticks.
            typeof(Game.App.Session.GameSession).GetProperty(nameof(f.Session.IsSandbox), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(f.Session, true);
            Assert.Throws<System.InvalidOperationException>(() => ((Game.Galaxy.Services.IShipSpawner)f.Spawner).Spawn(
                new Game.Galaxy.Services.GalaxyShipSpawnArgs { Blueprint = f.Blueprint.Id, Rotation = quaternion.identity }));
            result = f.Command("spawn blueprint=" + f.Blueprint.IdString + " x=9 z=10 ai=false");
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text); f.Test.StepTicks(1); f.Host.Update();
            Assert.IsTrue(f.Host.TryGetOperation(result.OperationId, out operation)); Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status);
            using var sandboxRoots = query.ToEntityArray(Allocator.Temp); int sandboxCount = 0;
            foreach (var root in sandboxRoots)
                if (root != f.Player && !f.Em.HasComponent<Game.Galaxy.Components.StarSystemMember>(root)) sandboxCount++;
            Assert.AreEqual(1, sandboxCount, "Only the explicitly sandbox-spawned NPC omits membership.");
        }
    }
}
