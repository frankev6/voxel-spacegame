using System;
using System.Collections;
using System.Collections.Generic;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Physics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine.TestTools;
using Fixture = Game.Tests.PlayMode.Simulation.Flight.FlightScenarioTests.Fixture;
using ReferenceScope = Game.Tests.PlayMode.Voxel.Physics.Perf.PhysicsPerfScope;

namespace Game.Tests.PlayMode.Simulation.Flight
{
    /// <summary>FLT-011: reference-setting flight costs at the specified population and geometry scale.</summary>
    public sealed class FlightPerfTests
    {
        const int Ships = 50, Warmup = 60, Samples = 300;
        ReferenceScope reference;
        [OneTimeSetUp] public void SetUp() => reference = new ReferenceScope();
        [OneTimeTearDown] public void TearDown() => reference?.Dispose();

        static Entity[] Populate(Fixture f, bool rebuild = false)
        {
            var roots = new Entity[Ships];
            byte aft = f.Content.Orientations.Value.AxisTurn(1, 2);
            for (int ship = 0; ship < Ships; ship++)
            {
                var source = BenchmarkStructures.Ship20k(f.Hull);
                for (int i = 0; i < 20; i++)
                    Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement
                    {
                        Block = f.Thruster, Origin = new int3(i * 4, 0, -1), Orient = aft
                    }, f.Content));
                if (rebuild)
                    for (int i = 0; i < 180; i++)
                        Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement
                        {
                            Block = f.Index("block_capacitor_small"), Origin = new int3(i % 30 * 2, 0, 28 + i / 30 * 2)
                        }, f.Content));
                Assert.AreEqual(rebuild ? 200 : 20, source.Data.Instances.Length);
                roots[ship] = f.SpawnSource(source, new float3(ship * 1000, 0, 0));
            }
            f.Ready(roots);
            foreach (var root in roots) Assert.AreEqual(20, f.Em.GetBuffer<FlightThruster>(root).Length);
            return roots;
        }
        static void Commands(Fixture f, Entity[] roots, int tick, bool revision = false)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                f.Command(roots[i], new float2(((tick + i) % 3 - 1) * .5f, tick % 80 < 60 ? .75f : -.5f), ((tick / 13 + i) % 3 - 1) * .6f);
                if (revision)
                {
                    var grid = f.Em.GetComponentData<VoxelGrid>(roots[i]); grid.Revision++; f.Em.SetComponentData(roots[i], grid);
                }
            }
        }
        static ProfilerRecorder Record(string name)
        {
            // IJobEntity uses named Burst Jobs markers in Entities 6.6, not the IJobParallelFor .Execute marker.
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var info = ProfilerRecorderHandle.GetDescription(handle);
                if (info.Name != name) continue;
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
                Assert.IsTrue(recorder.Valid, name); Assert.IsTrue(recorder.IsRunning, name); return recorder;
            }
            Assert.Fail("Missing profiler marker: " + name); return default;
        }
        static void Gate(string name, double[] samples, double budget, string unit = "ms")
        {
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName + "." + name).AddSamples(name, samples, unit);
            if (budget > 0) Assert.Greater(run.Get(name).Median, 0, "A missing profiler sample must not pass the gate");
            run.Write(name, budget); run.AssertMedian(name, budget);
            if (budget == 0) run.AssertMax(name, 0);
            TestContext.WriteLine($"{name}: median/p95 {run.Get(name).Median:G8}/{run.Get(name).P95:G8} {unit}, budget {budget}");
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Propulsion50Ships_Under0_15ms()
        {
            PerfSettings.AssertReference();
            using var f = new Fixture(); var roots = Populate(f);
            using var worker = Record("PropulsionJob (Burst)");
            var samples = new double[Samples];
            for (int i = 0; i < Warmup + Samples; i++)
            {
                Commands(f, roots, i); long before = worker.CurrentValue; f.Tick();
                double elapsed = (worker.CurrentValue - before) * 1e-6;
                if (i >= Warmup) samples[i - Warmup] = elapsed;
                yield return null;
            }
            Gate("Propulsion50Ships", samples, .15);
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Propulsion_MainThread()
        {
            PerfSettings.AssertReference();
            using var f = new Fixture(); var roots = Populate(f);
            f.Em.AddComponent<PlayerShip>(roots[0]);
            for (int i = 0; i < 50; i++)
            {
                var body = f.Em.CreateEntity(typeof(PlanarConstraint), typeof(LocalTransform), typeof(PhysicsVelocity), typeof(PhysicsMass));
                f.Em.SetComponentData(body, LocalTransform.FromPosition(new float3(-1000, 1, i)));
                f.Em.SetComponentData(body, new PhysicsMass { Transform = RigidTransform.identity, InverseMass = 1, InverseInertia = new float3(0, 1, 0) });
            }
            using var propulsion = Record("PropulsionSystem.Schedule");
            using var player = Record("PlayerCommandApplySystem.Apply");
            using var plane = Record("PlanarImpulseJob (Burst)");
            var propulsionSamples = new double[Samples]; var playerSamples = new double[Samples];
            var planeSamples = new double[Samples]; var gc = new double[Samples];
            for (int i = 0; i < Warmup + Samples; i++)
            {
                Commands(f, roots, i);
                f.Em.SetComponentData(f.InputEntity, new PlayerCommand { Thrust = new float2(.5f, .75f), Turn = i % 2 == 0 ? .5f : -.5f });
                long p0 = propulsion.CurrentValue, i0 = player.CurrentValue, c0 = plane.CurrentValue;
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                // Isolate only the three flight systems and completion of their jobs, excluding fixture mutation, scene/physics and test allocations.
                f.Test.UpdateSystem<PlayerCommandApplySystem>();
                f.Test.UpdateSystem<PropulsionSystem>();
                f.Test.UpdateSystem<PlanarConstraintSystem>();
                f.Test.CompleteAll();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                if (i >= Warmup)
                {
                    int k = i - Warmup;
                    propulsionSamples[k] = (propulsion.CurrentValue - p0) * 1e-6;
                    playerSamples[k] = (player.CurrentValue - i0) * 1e-6;
                    planeSamples[k] = (plane.CurrentValue - c0) * 1e-6; gc[k] = allocated;
                }
                yield return null;
            }
            Gate("PropulsionMain", propulsionSamples, .03); Gate("PlayerCommandApply", playerSamples, .01);
            Gate("Planar100Bodies", planeSamples, .02); Gate("FlightGc", gc, 0, "B");
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator ThrusterListRebuild()
        {
            PerfSettings.AssertReference();
            using var f = new Fixture(); var roots = Populate(f, rebuild: true);
            var config = FlightConfig.Default; config.AllocationSweeps = 0; f.Em.SetComponentData(f.ConfigEntity, config);
            using var worker = Record("PropulsionJob (Burst)");
            var samples = new double[Samples];
            for (int i = 0; i < Warmup + Samples; i++)
            {
                Commands(f, roots, i, revision: true); long before = worker.CurrentValue; f.Tick();
                // Conservative: includes the entire propulsion job (zero solver sweeps), not a subtraction of two noisy timings.
                if (i >= Warmup) samples[i - Warmup] = (worker.CurrentValue - before) * .001 / Ships;
                yield return null;
            }
            Gate("ThrusterRebuild200Instances", samples, 2, "us");
        }
    }
}
