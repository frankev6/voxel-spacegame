using Game.App.DevRuntime.Commands;
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
    /// <summary>DEV-031 (ADR-0123): the `flight3d` switch writes the request, and Flight3DModeSystem follows it.</summary>
    public sealed class Flight3DDevCommandTests : GameTestBase
    {
        [Test] public void Toggle_EnablesAndDisables()
        {
            var world = CreateWorld(typeof(Flight3DModeSystem));
            world.AddGameplayActive();
            var em = world.EntityManager;
            Assert.IsFalse(Flight3DDevCommand.TryApply(em, Entity.Null, true, out string text));
            StringAssert.Contains("no player ship", text);

            var ship = em.CreateEntity(typeof(Flight3D), typeof(Flight3DRequest), typeof(LocalTransform), typeof(PhysicsVelocity), typeof(PhysicsMass));
            em.SetComponentData(ship, LocalTransform.Identity);
            em.SetComponentData(ship, new PhysicsMass { InverseMass = 1, InverseInertia = new float3(0, 1, 0), Transform = RigidTransform.identity });
            world.StepTicks(1);
            Assert.IsFalse(em.IsComponentEnabled<Flight3D>(ship), "Added enabled at spawn; the mode system turns it off while the request is off.");
            Assert.IsTrue(Flight3DDevCommand.TryApply(em, ship, null, out text)); StringAssert.Contains("planar flight", text);

            Assert.IsTrue(Flight3DDevCommand.TryApply(em, ship, true, out text)); StringAssert.Contains("starting", text);
            world.StepTicks(1);
            Assert.IsTrue(em.IsComponentEnabled<Flight3D>(ship));
            Assert.IsTrue(Flight3DDevCommand.TryApply(em, ship, null, out text)); StringAssert.Contains("3D flight on", text);
            StringAssert.Contains("B face the mouse", text);

            Assert.IsTrue(Flight3DDevCommand.TryApply(em, ship, false, out text)); StringAssert.Contains("levelling", text);
            world.StepTicks(1);
            Assert.IsFalse(em.IsComponentEnabled<Flight3D>(ship), "A level ship goes straight back to the plane.");
        }
    }
}
