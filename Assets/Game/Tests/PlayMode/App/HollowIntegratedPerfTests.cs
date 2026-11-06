using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.App.DevRuntime;
using Game.Core.Components;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Entities;
using Unity.PerformanceTesting;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>TST-010: passive rendered Hollow capture; never substitutes a test camera or changes quality/simulation settings.</summary>
    public sealed class HollowIntegratedPerfTests : SessionCommandFixture
    {
        sealed class Recording : IDisposable
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public readonly double[] Values = new double[300];
            public int AvailableFrames;
            public void Dispose() { if (Recorder.Valid) Recorder.Dispose(); }
        }

        bool Ready()
        {
            if (!Host.Playground.Hollow.Built || !Camera.main) return false;
            using var spawning = EM.CreateEntityQuery(ComponentType.ReadOnly<GridSpawning>());
            if (!spawning.IsEmpty) return false;
            using var batches = EM.CreateEntityQuery(ComponentType.ReadOnly<BrickMeshBatch>());
            using var queues = EM.CreateEntityQuery(ComponentType.ReadOnly<BrickMeshQueue>());
            if (batches.IsEmpty || queues.IsEmpty || batches.GetSingleton<BrickMeshBatch>().InFlight != 0) return false;
            var queue = queues.GetSingleton<BrickMeshQueue>(); queue.CompleteAccess();
            if (queue.Count != 0) return false;
            var physics = GridColliderStats.Gather(EM);
            return physics.Grids > 0 && physics.PendingUpdates == 0 && physics.PendingBricks == 0;
        }

        static bool Wanted(string name)
        {
            if (name == "Main Thread" || name == "Render Thread" || name == "PlayerLoop") return true;
            string[] keys = { "Physics", "Propulsion", "Integrity", "BrickPool", "BrickCull", "BrickMesh", "PropRender", "GridMatrix",
                "GridDormancy", "BrickLod", "LodEnqueue", "LocalToWorld", "TransformJob", "SpatialHash", "ProxyGather", "ProxySort",
                "ProxyBucket", "AiThink", "DamageResolve", "ResolveGrid", "Collider", "EngineWash", "ZoneJob", "MineSystem" };
            foreach (var key in keys) if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static List<Recording> Discover(StringBuilder metadata)
        {
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var result = new List<Recording>(); var names = new HashSet<string>();
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (!Wanted(description.Name) || !names.Add(description.Name)) continue;
                // Time samples only: counters containing "Physics" must not be mislabelled as milliseconds.
                if (description.UnitType != Unity.Profiling.LowLevel.ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
                if (!recorder.Valid) { recorder.Dispose(); continue; }
                result.Add(new Recording { Name = description.Name, Recorder = recorder });
                metadata.AppendLine("marker=" + description.Name);
            }
            return result;
        }

        string Snapshot() => Host.Commands.Execute("perf voxel=true combat=true json=true", Host.Context).Text;
        uint Tick() { using var q = EM.CreateEntityQuery(ComponentType.ReadOnly<SimClock>()); return q.GetSingleton<SimClock>().Tick; }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Hollow_ProductionSettings_PassiveCapture()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Rendered Hollow benchmark requires a GPU; headless functional mode is not a performance result.");
            yield return RunCommand("playground");
            var move = Host.Commands.Execute("playground goto=hollow", Host.Context);
            Assert.AreEqual(DevResultKind.Ok, move.Kind, move.Text);
            yield return Until(Ready, "Hollow spawning, mesh queue and collider pipeline drained", 90);

            var camera = Camera.main;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline); Assert.IsTrue(camera.enabled);
            int width = camera.pixelWidth, height = camera.pixelHeight, msaa = pipeline.msaaSampleCount;
            float scale = pipeline.renderScale, shadows = pipeline.shadowDistance;
            var metadata = new StringBuilder();
            metadata.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
            metadata.AppendLine($"screen={Screen.width}x{Screen.height}; camera={width}x{height}; pipeline={pipeline.name}; MSAA={msaa}; renderScale={scale}; shadowDistance={shadows}");
            metadata.AppendLine($"quality={QualitySettings.GetQualityLevel()}; vsync={QualitySettings.vSyncCount}; targetFrameRate={Application.targetFrameRate}; graphics={SystemInfo.graphicsDeviceType}; device={SystemInfo.graphicsDeviceName}");
            metadata.AppendLine($"cameraPosition={camera.transform.position}; cameraRotation={camera.transform.rotation}; FOV={camera.fieldOfView}; hdr={camera.allowHDR}; msaaAllowed={camera.allowMSAA}; postProcessing={camera.GetUniversalAdditionalCameraData().renderPostProcessing}");
            metadata.AppendLine("referencePerfSettings=" + PerfSettings.IsReference(out var settingsReason) + "; " + settingsReason);
            metadata.AppendLine("before=" + Snapshot()); // Completes jobs and walks bricks: intentionally outside measurement.
            var recordings = Discover(metadata);
            var frame = new double[300]; var gpu = new List<double>(300); var timings = new FrameTiming[1];
            bool timingEnabled = FrameTimingManager.IsFeatureEnabled(); ulong previousTimestamp = 0;
            uint firstTick = 0;
            try
            {
                Assert.IsTrue(recordings.Exists(r => r.Name == "PlayerLoop" || r.Name == "Main Thread"), "No inclusive CPU timing handle available");
                double deadline = Time.realtimeSinceStartupAsDouble + 90;
                for (int i = 0; i < 60; i++) { if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("Warmup deadline"); yield return null; }
                firstTick = Tick(); // Read once and discard its frame before measuring.
                yield return null;
                for (int i = 0; i < 300; i++)
                {
                    if (timingEnabled) FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("Capture deadline");
                    frame[i] = Time.unscaledDeltaTime * 1000.0;
                    foreach (var recording in recordings)
                    {
                        if (recording.Recorder.Count == 0) continue;
                        recording.AvailableFrames++;
                        recording.Values[i] = recording.Recorder.LastValue * 1e-6;
                    }
                    if (timingEnabled && FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0
                        && timings[0].frameStartTimestamp != previousTimestamp)
                    { previousTimestamp = timings[0].frameStartTimestamp; gpu.Add(timings[0].gpuFrameTime); }
                }
                uint endTick = Tick();
                Assert.Greater(endTick, firstTick, "Simulation must advance during capture");
                Assert.AreEqual(width, camera.pixelWidth); Assert.AreEqual(height, camera.pixelHeight);
                Assert.AreEqual(msaa, pipeline.msaaSampleCount); Assert.AreEqual(scale, pipeline.renderScale); Assert.AreEqual(shadows, pipeline.shadowDistance);
                metadata.AppendLine($"ticks={firstTick}..{endTick}; GPUFrameTimingEnabled={timingEnabled}; uniquePositiveGpuSamples={gpu.Count}");
                metadata.AppendLine("after=" + Snapshot());
                var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("RenderedFrameInterval", frame);
                foreach (var recording in recordings)
                {
                    if (recording.AvailableFrames == 0) { metadata.AppendLine("unavailable=" + recording.Name); continue; }
                    run.AddSamples(recording.Name, recording.Values);
                    var stat = run.Get(recording.Name);
                    metadata.AppendLine($"{recording.Name}: median={stat.Median:F6} p95={stat.P95:F6} ms; availableFrames={recording.AvailableFrames}/300");
                }
                if (gpu.Count > 0) run.AddSamples("GPUFrameTimingDiagnostic", gpu);
                else metadata.AppendLine("GPU timing unavailable; no GPU bottleneck conclusion is supported.");
                Assert.Greater(run.Get("RenderedFrameInterval").Median, 0);
                run.Write();
                string path = Path.ChangeExtension(PerfRun.PathFor(TestContext.CurrentContext.Test.FullName), ".metadata.txt");
                File.WriteAllText(path, metadata.ToString());
                TestContext.WriteLine(metadata.ToString()); TestContext.WriteLine("Metadata: " + path);
                TestContext.WriteLine("Includes real production traffic/rendering and Editor overhead. Marker times overlap and must not be summed into a frame estimate. No whole-game speedup is asserted by this capture.");
            }
            finally { foreach (var recording in recordings) recording.Dispose(); }
        }
    }
}
