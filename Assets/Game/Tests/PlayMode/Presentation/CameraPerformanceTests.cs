using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Input;
using Game.Presentation.Data;
using Game.Presentation.Input;
using Game.Presentation.Services;
using Game.Presentation.Systems;
using Game.Simulation.Commands;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Physics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using ReferenceScope = Game.Tests.PlayMode.Voxel.Physics.Perf.PhysicsPerfScope;

namespace Game.Tests.PlayMode.Presentation
{
    /// <summary>CAM-011: inclusive bridge/rig gates and a ten-second, 80 m/s scripted circle.</summary>
    public sealed class CameraPerformanceTests
    {
        const int Warmup = 60, Samples = 300;
        const float Dt = 1f / 60;
        ReferenceScope reference;
        [OneTimeSetUp] public void SetUpReference() => reference = new ReferenceScope();
        [OneTimeTearDown] public void TearDownReference() => reference?.Dispose();
        [TearDown] public void TearDown() => LogAssert.NoUnexpectedReceived();

        sealed class Fixture : IDisposable
        {
            internal readonly EcsTestWorld Test = EcsTestWorld.Create(typeof(CameraTargetSyncSystem));
            readonly GameObject parent = new GameObject("Camera gate parent");
            readonly GameObject prefab = new GameObject("Camera gate prefab", typeof(Camera));
            readonly CameraConfig config = ScriptableObject.CreateInstance<CameraConfig>();
            readonly InputService input = new InputService(null);
            internal readonly CameraRigService Rig;
            internal readonly CameraTargetSyncSystem Sync;
            readonly Entity ship;
            internal Fixture()
            {
                prefab.GetComponent<Camera>().enabled = false; config.CameraPrefab = prefab;
                input.SetContext(InputContext.Flight);
                Rig = new CameraRigService(config, parent.transform, new InputCameraInput(input));
                Rig.SetMode(CameraMode.Flight, true);
                var em = Test.EntityManager;
                em.CreateSingleton<VoxelViewer>();
                ship = em.CreateEntity(typeof(PlayerShip), typeof(LocalToWorld), typeof(PhysicsVelocity), typeof(VoxelGrid));
                em.SetComponentData(ship, new VoxelGrid { BrickDims = new int3(2, 1, 3), CellSize = 1 });
                Sync = Test.World.GetExistingSystemManaged<CameraTargetSyncSystem>(); Sync.Bind(Rig, null);
                Circle(0); Sync.Update();
            }
            internal void Circle(float seconds)
            {
                // One complete circle in ten seconds, tangent speed exactly 80 m/s.
                const float omega = 2 * math.PI / 10, radius = 80 / omega;
                math.sincos(seconds * omega, out float sin, out float cos);
                float3 position = new float3(radius * cos, 0, radius * sin), velocity = new float3(-80 * sin, 0, 80 * cos);
                quaternion rotation = quaternion.RotateY(math.atan2(velocity.x, velocity.z));
                Test.EntityManager.SetComponentData(ship, new LocalToWorld { Value = float4x4.TRS(position, rotation, new float3(1)) });
                Test.EntityManager.SetComponentData(ship, new PhysicsVelocity { Linear = velocity });
            }
            public void Dispose()
            {
                Test.Dispose(); Rig.Dispose(); input.Dispose();
                Object.DestroyImmediate(parent); Object.DestroyImmediate(prefab); Object.DestroyImmediate(config);
            }
        }

        static ProfilerRecorder Record(Fixture f)
        {
            string name = EntityManager.EntityManagerDebug.GetSystemProfilerMarkerName(f.Test.World, f.Sync.SystemHandle);
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                if (ProfilerRecorderHandle.GetDescription(handle).Name != name) continue;
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
                Assert.IsTrue(recorder.Valid, name); Assert.IsTrue(recorder.IsRunning, name); return recorder;
            }
            Assert.Fail("Missing inclusive camera system marker: " + name); return default;
        }
        static void Gate(string name, double[] samples, double budget, string unit = "ms")
        {
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName + "." + name).AddSamples(name, samples, unit);
            if (budget > 0) Assert.Greater(run.Get(name).Median, 0, "Missing timings cannot pass");
            run.Write(name, budget); run.AssertMedian(name, budget);
            if (budget == 0) run.AssertMax(name, 0);
            TestContext.WriteLine($"{name}: median/p95 {run.Get(name).Median:G8}/{run.Get(name).P95:G8} {unit}, budget {budget}");
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator FlightFrame_Under0_15ms()
        {
            PerfSettings.AssertReference(); using var f = new Fixture(); using var recorder = Record(f);
            var samples = new double[Samples];
            for (int i = 0; i < Warmup + Samples; i++)
            {
                yield return null; f.Circle(i * Dt);
                long before = recorder.CurrentValue; f.Sync.Update();
                if (i >= Warmup) samples[i - Warmup] = (recorder.CurrentValue - before) * 1e-6;
            }
            // Inclusive system marker contains Tick: never add its nested marker a second time.
            // SPEC-62 Q14: M1 has no occlusion yet; enforce the stricter 0.05 ms gate now.
            Gate("CameraWithoutOcclusion", samples, .05);
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator FlightFrame_ZeroGc()
        {
            PerfSettings.AssertReference(); using var f = new Fixture(); var samples = new double[Samples];
            for (int i = 0; i < Warmup + Samples; i++)
            {
                yield return null; f.Circle(i * Dt);
                long before = GC.GetAllocatedBytesForCurrentThread(); f.Sync.Update();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (i >= Warmup) samples[i - Warmup] = allocated;
            }
            Gate("CameraGc", samples, 0, "B");
        }

        [UnityTest, Performance, Category("Performance"), Category("Scenario")]
        public IEnumerator CircleFlight_PitchInRangeEveryFrame()
        {
            PerfSettings.AssertReference(); using var f = new Fixture();
            for (int i = 0; i < Warmup; i++) { yield return null; f.Sync.Update(); }
            var gc = new List<double>(2048); float minPitch = 90, maxPitch = 0, elapsed = 0;
            double timeout = Time.realtimeSinceStartupAsDouble + 45;
            while (elapsed < 10)
            {
                yield return null;
                Assert.Less(Time.realtimeSinceStartupAsDouble, timeout, "Camera scenario must advance within the real-time limit");
                // captureDeltaTime only controls scaled time here. Advance this presentation
                // scenario with exactly the unscaled, clamped dt read by the production bridge.
                elapsed += math.clamp(Time.unscaledDeltaTime, 0, .1f);
                f.Circle(elapsed);
                long before = GC.GetAllocatedBytesForCurrentThread(); f.Sync.Update();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                float pitch = math.degrees(math.asin(-f.Rig.Camera.transform.forward.y));
                Assert.That(pitch, Is.InRange(35f - 1e-4f, 80f + 1e-4f));
                gc.Add(allocated); minPitch = math.min(minPitch, pitch); maxPitch = math.max(maxPitch, pitch);
            }
            Assert.GreaterOrEqual(gc.Count, Samples, "At least 300 measured frames on the reference machine");
            Gate("CircleCameraGc", gc.ToArray(), 0, "B");
            TestContext.WriteLine($"Circle: {gc.Count} frames / {elapsed:G8} s / 80 m/s, pitch range {minPitch:G8}–{maxPitch:G8} degrees");
        }
    }
}
