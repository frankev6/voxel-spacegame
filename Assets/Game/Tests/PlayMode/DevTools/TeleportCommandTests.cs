using System;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.Core.Components;
using Game.Galaxy.Components;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Tests.PlayMode.DevTools
{
    public sealed class TeleportCommandTests
    {
        [Test] public void SetsPositionZeroVelocity()
        {
            using var f = new WorldCommandFixture(true); var em = f.Em; var root = f.Player;
            var velocity = new PhysicsVelocity { Linear = new float3(5, 0, -2), Angular = new float3(0, 1, 0) };
            em.SetComponentData(root, velocity);
            em.SetComponentData(root, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = new RigidTransform(quaternion.identity, new float3(-50, 0, 80)), PreviousVelocity = velocity });
            em.SetComponentData(root, new PhysicsGraphicalSmoothing { CurrentVelocity = velocity, ApplySmoothing = 1 });
            var result = f.Command("teleport x=100 z=-50"); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            var expected = new float3(100, 0, -50);
            Assert.AreEqual(expected, em.GetComponentData<LocalTransform>(root).Position);
            Assert.AreEqual(default(PhysicsVelocity), em.GetComponentData<PhysicsVelocity>(root));
            Assert.AreEqual(expected, em.GetComponentData<PhysicsGraphicalInterpolationBuffer>(root).PreviousTransform.pos);
            Assert.AreEqual(default(PhysicsVelocity), em.GetComponentData<PhysicsGraphicalInterpolationBuffer>(root).PreviousVelocity);
            Assert.AreEqual(0, em.GetComponentData<PhysicsGraphicalSmoothing>(root).ApplySmoothing);
            for (int tick = 0; tick < 2; tick++)
            {
                f.Test.StepTicks(1);
                Assert.That(math.distance(expected, em.GetComponentData<LocalTransform>(root).Position), Is.LessThan(1e-5f));
                Assert.AreEqual(default(PhysicsVelocity), em.GetComponentData<PhysicsVelocity>(root));
            }
            Assert.IsTrue(DevEcs.TryGetUniversePosition(f.Host.Context, root, out var up));
            Assert.AreEqual(WorldCommandFixture.Origin + (double3)expected, up);
            if (em.HasComponent<UniversePosition>(root)) em.SetComponentData(root, new UniversePosition { Value = up });
            else em.AddComponentData(root, new UniversePosition { Value = up });
            result = f.Command("teleport sx=1250 sz=-2300 yaw=90"); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            Assert.AreEqual(new float3(250, 0, -300), em.GetComponentData<LocalTransform>(root).Position);
            Assert.AreEqual(new double3(1250, 0, -2300), em.GetComponentData<UniversePosition>(root).Value);
            Assert.Greater(math.dot(math.mul(em.GetComponentData<LocalTransform>(root).Rotation, new float3(0, 0, 1)), new float3(1, 0, 0)), .999f);
            Assert.IsTrue(f.Host.Probes.TryGet("player.upos.x", out var probe));
            Assert.AreEqual(1250, probe.Read(f.Host.Context, ReadOnlySpan<ScenarioValue>.Empty).Number);
            Assert.AreEqual(DevResultKind.Failed, f.Command("teleport x=1").Kind);
            Assert.AreEqual(DevResultKind.Failed, f.Command("teleport x=1 z=2 sx=3 sz=4").Kind);
        }
    }
}
