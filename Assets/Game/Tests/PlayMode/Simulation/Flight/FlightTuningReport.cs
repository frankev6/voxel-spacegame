using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation.Flight;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Fixture = Game.Tests.PlayMode.Simulation.Flight.FlightScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Flight
{
    /// <summary>FLT-010 measured legacy flight table; thresholds are tuning evidence, not pass/fail assertions.</summary>
    public sealed class FlightTuningReport
    {
        const int TurnTicks = 600, MaxTicks = 7200, AccelerationTicks = 15;
        Scene scene;
        [SetUp] public void SetUp() => scene = SceneManager.CreateScene("FLT-010 legacy tuning");
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var unload = SceneManager.UnloadSceneAsync(scene); float until = Time.realtimeSinceStartup + 10;
            while (!unload.isDone && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(unload.isDone);
        }
        static string TimeText(float time, bool available, float limit)
            => !available ? "no forward thrust" : float.IsPositiveInfinity(time) ? ">" + limit : time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        [UnityTest]
        public IEnumerator PrintTable()
        {
            using var content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            var blueprints = new List<ShipBlueprintDefinition>(content.Catalog.All<ShipBlueprintDefinition>());
            blueprints.Sort((a, b) => StringComparer.Ordinal.Compare(a.IdString, b.IdString));
            var configured = FlightConfigFactory.Create(content.Catalog.Get<FlightConfigDefinition>(new ContentId("config_flight")));
            var configurations = configured.Equals(FlightConfig.Default) ? new[] { configured } : new[] { FlightConfig.Default, configured };
            int measured = 0;
            foreach (var config in configurations)
            {
                TestContext.WriteLine(FormattableString.Invariant($"FLT-010 config: forward={config.MaxForwardSpeed}, yawCap={config.MaxYawRateDeg}, coreAccel={config.CoreYawAccelDeg}, coreTorque={config.CoreMaxYawTorque}"));
                TestContext.WriteLine("| Blueprint | Cells | Mass kg | Iyy kg m² | Forward N | Thrusters S/M/L | Accel m/s² (0–0.25 s) | t99% cap s | Turn deg/s (10 s) | t90 s | Peak forward m/s |");
                TestContext.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
                foreach (var definition in blueprints)
                {
                    using var f = new Fixture(); f.Em.SetComponentData(f.ConfigEntity, config);
                    var straight = f.SpawnBlueprint(definition); var turning = f.SpawnBlueprint(definition, new float3(1000, 0, 0));
                    f.Ready(straight, turning);
                    var mass = f.Mass(straight);
                    double massKg = f.Em.GetComponentData<GridMass>(straight).Data.Mass;
                    float inertia = mass.InverseInertia.y > 0 ? 1 / mass.InverseInertia.y : 0;
                    var thrusters = f.Em.GetBuffer<FlightThruster>(straight); int count = thrusters.Length;
                    float forwardForce = 0; int small = 0, medium = 0, large = 0;
                    var instances = f.Em.GetBuffer<BlockInstance>(straight);
                    ushort smallId = f.Index("block_thruster_small"), mediumId = f.Index("block_thruster_medium"), largeId = f.Index("block_thruster_large");
                    foreach (var thruster in thrusters)
                    {
                        forwardForce += thruster.MaxForce * math.max(0, thruster.ForceDirLocal.z);
                        ushort block = instances[thruster.InstanceIndex].Block;
                        if (block == smallId) small++; else if (block == mediumId) medium++; else if (block == largeId) large++;
                    }
                    float acceleration = 0, speedTime = float.PositiveInfinity, turnTime = float.PositiveInfinity, turnRate = 0;
                    float angle = 0, previousYaw = 0, peakSpeed = 0;
                    f.Command(straight, new float2(0, 1)); f.Command(turning, turn: 1);
                    for (int tick = 1; tick <= MaxTicks; tick++)
                    {
                        f.Tick();
                        float speed = FlightMath.ToLocalPlanar(f.Velocity(straight).Linear, f.Transform(straight).Rotation).y;
                        peakSpeed = math.max(peakSpeed, speed);
                        if (tick == AccelerationTicks) acceleration = speed / (AccelerationTicks * SimTime.FixedTimestep);
                        if (float.IsPositiveInfinity(speedTime) && speed >= config.MaxForwardSpeed * .99f) speedTime = tick * SimTime.FixedTimestep;
                        if (tick <= TurnTicks)
                        {
                            float yaw = Planar.YawFromForward(math.rotate(f.Transform(turning).Rotation, new float3(0, 0, 1)));
                            angle += Planar.WrapAngle(yaw - previousYaw); previousYaw = yaw;
                            if (float.IsPositiveInfinity(turnTime) && angle >= math.PI * .5f) turnTime = tick * SimTime.FixedTimestep;
                            if (tick == TurnTicks) turnRate = math.degrees(f.Velocity(turning).Angular.y);
                        }
                        if (tick >= TurnTicks && (!float.IsPositiveInfinity(speedTime) || forwardForce == 0)) break;
                        if (tick % 30 == 0) yield return null;
                    }
                    TestContext.WriteLine(FormattableString.Invariant($"| {definition.IdString} | {definition.CellCount} | {massKg:F1} | {inertia:F1} | {forwardForce:F0} | {count} ({small}/{medium}/{large}) | {acceleration:F3} | {TimeText(speedTime, forwardForce > 0, 120)} | {turnRate:F3} | {TimeText(turnTime, true, 10)} | {peakSpeed:F3} |"));
                    measured++;
                    yield return null;
                }
            }
            Assert.Greater(blueprints.Count, 0);
            Assert.AreEqual(blueprints.Count * configurations.Length, measured, "Every catalog blueprint spawned and was measured for each configuration");
        }
    }
}
