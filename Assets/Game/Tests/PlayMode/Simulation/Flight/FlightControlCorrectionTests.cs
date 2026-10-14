using Game.App.Boot;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Game.Tests.PlayMode.Simulation.Flight
{
    /// <summary>FLT-013: actual imported Fighter, production thrusters and Unity Physics; QA-001/ADR-0114 core manoeuvring assist.</summary>
    public sealed class FlightControlCorrectionTests
    {
        ContentService content;
        FlightScenarioTests.Fixture fixture;
        Entity ship;

        [SetUp] public void SetUp()
        {
            content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            fixture = new FlightScenarioTests.Fixture();
            fixture.Em.SetComponentData(fixture.ConfigEntity,
                FlightConfigFactory.Create(content.Catalog.Get<FlightConfigDefinition>(new ContentId("config_flight"))));
            ship = fixture.SpawnBlueprint(content.Catalog.Get<ShipBlueprintDefinition>(new ContentId("ship_fighter")));
            fixture.Ready(ship);
        }
        [TearDown] public void TearDown() { fixture?.Dispose(); content?.Dispose(); }

        void ResetMotion(quaternion rotation)
        {
            var transform = fixture.Transform(ship); transform.Rotation = rotation;
            fixture.Em.SetComponentData(ship, transform);
            fixture.Em.SetComponentData(ship, default(PhysicsVelocity));
            var thrusters = fixture.Em.GetBuffer<FlightThruster>(ship);
            for (int i = 0; i < thrusters.Length; i++) { var t = thrusters[i]; t.Throttle = 0; thrusters[i] = t; }
            var state = fixture.Em.GetComponentData<FlightState>(ship); state.Gyro = 0; fixture.Em.SetComponentData(ship, state);
        }

        [Test] public void Fighter_DirectionalThrust()
        {
            foreach (var direction in new[] { new float2(0, 1), new float2(0, -1), new float2(1, 0), new float2(-1, 0) })
            {
                ResetMotion(quaternion.RotateY(math.PI / 2));
                float available = 0;
                foreach (var thruster in fixture.Em.GetBuffer<FlightThruster>(ship))
                    available += thruster.MaxForce * math.max(0, math.dot(thruster.ForceDirLocal, new float3(direction.x, 0, direction.y)));
                Assert.That(available, Is.EqualTo(direction.y > 0 ? 180000 : 40000));
                fixture.Command(ship, direction); fixture.Tick(60);
                // Compare in the original body frame: +Z became world +X at the 90-degree test rotation.
                float2 velocity = FlightMath.ToLocalPlanar(fixture.Velocity(ship).Linear, quaternion.RotateY(math.PI / 2));
                float acceleration = math.dot(velocity, direction);
                float core = FlightMath.CoreAssistForce(fixture.Em.GetComponentData<FlightConfig>(fixture.ConfigEntity), fixture.Mass(ship).InverseMass);
                Assert.Greater(core, 0, "Production config enables the core manoeuvring assist.");
                float bound = (available + core) * fixture.Mass(ship).InverseMass;
                Assert.That(acceleration, Is.InRange(bound * .9f, bound * 1.01f), direction.ToString());
                Assert.Less(math.abs(velocity.x * direction.y - velocity.y * direction.x), .05f);
                TestContext.WriteLine($"Fighter {direction}: {acceleration:G8} m/s²; force/mass {bound:G8}, velocity {velocity}");
            }
            ResetMotion(quaternion.identity);
            var thrustersCopy = fixture.Em.GetBuffer<FlightThruster>(ship).ToNativeArray(Unity.Collections.Allocator.Temp);
            foreach (var thruster in thrustersCopy)
                if (thruster.ForceDirLocal.z < -.5f) fixture.RemoveThruster(ship, thruster.InstanceIndex);
            thrustersCopy.Dispose();
            fixture.Command(ship, new float2(0, -1)); fixture.Tick(60);
            float coreOnly = FlightMath.CoreAssistForce(fixture.Em.GetComponentData<FlightConfig>(fixture.ConfigEntity), fixture.Mass(ship).InverseMass) * fixture.Mass(ship).InverseMass;
            Assert.That(-fixture.Velocity(ship).Linear.z, Is.InRange(coreOnly * .9f, coreOnly * 1.01f),
                "After losing both reverse thrusters only the core assist reverses; no thruster force is invented.");
        }

        [Test] public void Fighter_TurnAndStrafe()
        {
            fixture.Command(ship, turn: 1); fixture.Tick(60);
            float heading = math.atan2(math.rotate(fixture.Transform(ship).Rotation, new float3(0, 0, 1)).x,
                math.rotate(fixture.Transform(ship).Rotation, new float3(0, 0, 1)).z);
            Assert.Greater(heading, .5f, "D must physically rotate the nose toward +X.");
            fixture.Em.SetComponentData(ship, new ShipCommand { Flags = ShipCommandFlags.Brake }); fixture.Tick(240);
            Assert.Less(math.abs(fixture.Velocity(ship).Angular.y), .01f);
            var rotation = fixture.Transform(ship).Rotation;
            var before = fixture.Velocity(ship).Linear;
            fixture.Command(ship, new float2(1, 0)); fixture.Tick(120);
            var delta = FlightMath.ToLocalPlanar(fixture.Velocity(ship).Linear - before, rotation);
            float angle = math.degrees(math.acos(math.clamp(math.abs(math.dot(rotation.value, fixture.Transform(ship).Rotation.value)), 0, 1))) * 2;
            Assert.Greater(delta.x, 2.5f);
            Assert.Less(angle, 2, "Strafe should hold facing with the Fighter's available gyro/thrusters.");
            TestContext.WriteLine($"Turn then strafe: lateral delta {delta.x:G8} m/s, facing drift {angle:G8} degrees");
        }

        [Test] public void Fighter_CoastAndBrake()
        {
            ResetMotion(quaternion.identity);
            fixture.Em.SetComponentData(ship, new PhysicsVelocity { Linear = new float3(0, 0, 20) });
            fixture.Command(ship); fixture.Tick(120);
            float coast = fixture.Velocity(ship).Linear.z;
            Assert.That(coast, Is.InRange(2f, 4f), "Owner 2026-10-14: releasing W/S must not drift indefinitely (coast 1/s).");
            ResetMotion(quaternion.identity);
            fixture.Em.SetComponentData(ship, new PhysicsVelocity { Linear = new float3(0, 0, 20) });
            fixture.Em.SetComponentData(ship, new ShipCommand { Flags = ShipCommandFlags.Brake }); fixture.Tick(120);
            float brake = fixture.Velocity(ship).Linear.z;
            Assert.Less(brake, math.min(coast - .2f, .5f));
            Assert.GreaterOrEqual(brake, -.05f, "Brake stops without reversing.");
            fixture.Tick(900); Assert.Less(math.length(fixture.Velocity(ship).Linear), .02f);
            TestContext.WriteLine($"From 20 m/s after 2 seconds: coast {coast:G8}, brake {brake:G8} m/s; final {fixture.Velocity(ship).Linear}");
        }
    }
}
