using System;
using System.Collections.Generic;
using Game.Data.Components;
using Game.Simulation.Flight;
using Game.Tests.Common;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-008: the impulse constraint followed by actual Unity Physics integration.</summary>
    public sealed class PlanarConstraintTests : GameTestBase
    {
        EcsTestWorld world;
        EntityManager EM => world.EntityManager;
        [SetUp]
        public void SetUp()
        {
            var systems = new List<Type> { typeof(PropulsionSystem), typeof(PlanarConstraintSystem) };
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics") || type.Namespace == "Unity.Transforms")) systems.Add(type);
            world = CreateWorld(systems.ToArray());
            world.AddGameplayActive(); world.AddSingleton(FlightConfig.Default);
            var step = PhysicsStep.Default; step.Gravity = float3.zero; world.AddSingleton(step);
        }
        Entity Body(bool planar = true)
        {
            var entity = EM.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsVelocity), typeof(PhysicsMass));
            EM.AddSharedComponent(entity, new PhysicsWorldIndex(0));
            EM.SetComponentData(entity, LocalTransform.FromPosition(0, 1, 0));
            EM.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(0, 5, 0) });
            EM.SetComponentData(entity, new PhysicsMass { Transform = RigidTransform.identity, InverseMass = .1f,
                InverseInertia = new float3(0, .2f, 0) });
            if (planar) EM.AddComponent<PlanarConstraint>(entity);
            return entity;
        }
        [Test]
        public void ReturnsToPlane()
        {
            var body = Body(); var hulk = Body(); EM.AddComponent<Hulk>(hulk);
            world.StepTicks(60);
            foreach (var entity in new[] { body, hulk })
            {
                var transform = EM.GetComponentData<LocalTransform>(entity);
                Assert.Less(math.abs(transform.Position.y), .001f);
                Assert.AreEqual(0, transform.Rotation.value.x); Assert.AreEqual(0, transform.Rotation.value.z);
                Assert.Less(math.abs(EM.GetComponentData<PhysicsVelocity>(entity).Linear.y), .001f);
            }
        }
        [Test]
        public void NoTilt()
        {
            var body = Body(); var mass = EM.GetComponentData<PhysicsMass>(body);
            var transform = EM.GetComponentData<LocalTransform>(body); var velocity = EM.GetComponentData<PhysicsVelocity>(body);
            // This off-centre impulse would pitch/roll an unconstrained inertia tensor.
            velocity.ApplyImpulse(mass, transform.Position, transform.Rotation, new float3(20, 5, 30), new float3(1, 3, 2));
            EM.SetComponentData(body, velocity);
            for (int i = 0; i < 60; i++)
            {
                world.StepTicks(1);
                transform = EM.GetComponentData<LocalTransform>(body); velocity = EM.GetComponentData<PhysicsVelocity>(body);
                Assert.AreEqual(0, transform.Rotation.value.x); Assert.AreEqual(0, transform.Rotation.value.z);
                Assert.AreEqual(0, velocity.Angular.x); Assert.AreEqual(0, velocity.Angular.z);
            }
            Assert.Greater(math.abs(velocity.Angular.y), .1f);
        }
        [Test]
        public void NonPlanar_Untouched()
        {
            var free = Body(false); var kinematic = Body();
            var mass = EM.GetComponentData<PhysicsMass>(kinematic); mass.InverseMass = 0; EM.SetComponentData(kinematic, mass);
            world.StepTicks(60);
            foreach (var entity in new[] { free, kinematic })
            {
                Assert.AreEqual(new float3(0, 5, 0), EM.GetComponentData<PhysicsVelocity>(entity).Linear);
                Assert.That(EM.GetComponentData<LocalTransform>(entity).Position.y, Is.EqualTo(6).Within(.0001f));
            }
        }
    }
}
