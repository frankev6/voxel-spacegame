using Game.App.DevRuntime.Commands;
using Game.Core.Net;
using Game.Data.Components;
using Game.Simulation.Flight;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.App
{
    /// <summary>DEV-031 (ADR-0123, amended 2026-11-08): `flight3d` writes the world switch, which starts on, and Flight3DModeSystem
    /// follows it on every ship; co-op counts as off.</summary>
    public sealed class Flight3DDevCommandTests : GameTestBase
    {
        static Entity Ship(EntityManager em)
        {
            var ship = em.CreateEntity(typeof(Flight3D), typeof(LocalTransform), typeof(PhysicsVelocity), typeof(PhysicsMass));
            em.SetComponentData(ship, LocalTransform.Identity);
            em.SetComponentData(ship, new PhysicsMass { InverseMass = 1, InverseInertia = new float3(1), Transform = RigidTransform.identity });
            return ship;
        }

        [Test] public void Toggle_EnablesAndDisables()
        {
            var world = CreateWorld(typeof(Flight3DModeSystem));
            world.AddGameplayActive();
            var em = world.EntityManager;
            var player = Ship(em); var npc = Ship(em);
            em.SetComponentEnabled<Flight3D>(npc, false);
            world.StepTicks(1);
            Assert.IsTrue(em.IsComponentEnabled<Flight3D>(player), "On by default.");
            Assert.IsTrue(em.IsComponentEnabled<Flight3D>(npc), "Every ship follows the switch, not only the player's.");
            Assert.IsTrue(Flight3DDevCommand.TryApply(em, player, null, out string text)); StringAssert.Contains("3D flight on for every ship", text);
            StringAssert.Contains("B face the mouse", text);
            Assert.IsTrue(Flight3DDevCommand.TryApply(em, Entity.Null, null, out text), "The switch works without a player ship.");

            Assert.IsTrue(Flight3DDevCommand.TryApply(em, player, false, out text)); StringAssert.Contains("ships level", text);
            world.StepTicks(1);
            Assert.IsFalse(em.IsComponentEnabled<Flight3D>(player), "A level ship goes straight back to the plane.");
            Assert.IsFalse(em.IsComponentEnabled<Flight3D>(npc));
            Assert.IsTrue(Flight3DDevCommand.TryApply(em, player, null, out text)); StringAssert.Contains("planar flight", text);

            Assert.IsTrue(Flight3DDevCommand.TryApply(em, player, true, out text)); StringAssert.Contains("starting", text);
            world.StepTicks(1);
            Assert.IsTrue(em.IsComponentEnabled<Flight3D>(player)); Assert.IsTrue(em.IsComponentEnabled<Flight3D>(npc));

            em.CreateSingleton(new SimRole { Value = SimRoleKind.Host });
            world.StepTicks(1);
            Assert.IsFalse(em.IsComponentEnabled<Flight3D>(player), "Co-op is planar even with the switch on.");
        }

        [Test] public void NoSwitch_Fails()
        {
            var world = CreateWorld();
            Assert.IsFalse(Flight3DDevCommand.TryApply(world.EntityManager, Entity.Null, true, out string text));
            StringAssert.Contains("no experimental 3D flight switch", text);
        }
    }
}
