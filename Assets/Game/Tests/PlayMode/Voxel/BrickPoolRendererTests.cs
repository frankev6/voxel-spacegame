using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.App.Boot;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Voxel
{
    /// <summary>REN-003: exact range packing, registered/indirect pixel parity, and the original 2500-brick gates.</summary>
    public sealed class BrickPoolRendererTests
    {
        readonly List<Object> owned = new List<Object>();
        readonly List<Camera> pausedCameras = new List<Camera>();
        BrickPoolRenderer pool;
        Camera camera;
        Light key;
        RenderTexture target;
        Material reference, indirect;
        ComputeShader copy;
        UniversalRenderPipelineAsset pipeline;
        RenderPipelineAsset previousPipeline;
        AmbientMode previousAmbientMode;
        Color previousAmbient;
        float previousReflection;
        bool previousAsync, previousSafety, initialized;
        int previousVsync, previousTargetRate;
        Action<ScriptableRenderContext, Camera> draw;
        World world;
        Entity root;
        EntityQuery bridgeQuery;
        uint frame;
        bool moving;
        double lastCpu;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        [Test] public void PageAllocator_NoOverlap_Fuzz()
        {
            var allocator = new PageAllocator(4096, 4096); var live = new List<PoolRange>();
            var random = new Unity.Mathematics.Random(7319);
            try
            {
                for (int step = 0; step < 10000; step++)
                {
                    if (live.Count > 0 && (live.Count > 180 || random.NextBool()))
                    { int i = random.NextInt(live.Count); allocator.Free(live[i]); live.RemoveAt(i); }
                    else
                    {
                        int count = random.NextInt(1, 8000);
                        if (!allocator.TryAllocate(count, out var range)) { allocator.GrowTo(allocator.Capacity + count); Assert.IsTrue(allocator.TryAllocate(count, out range)); }
                        Assert.AreEqual(count, range.Count, "No per-brick page padding");
                        foreach (var other in live) Assert.IsTrue(range.End <= other.Start || other.End <= range.Start, "Ranges overlap");
                        live.Add(range);
                    }
                    Assert.AreEqual(live.Sum(r => r.Count), allocator.Used);
                }
                foreach (var range in live) allocator.Free(range);
                Assert.IsTrue(allocator.TryAllocate(allocator.Capacity, out var whole), "Adjacent frees must coalesce");
                Assert.AreEqual(0, whole.Start); allocator.Free(whole); Assert.AreEqual(0, allocator.Used);
                TestContext.WriteLine("10,000 allocation/free operations; exact ranges, no overlap, full coalescence; capacity=" + allocator.Capacity);
            }
            finally { allocator.Dispose(); }
        }
        IEnumerator Setup(int count, bool perf)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU gate: run without -nographics, with -force-gfx-mt and frame timing enabled (08-tools).");
            initialized = true; previousAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            previousSafety = BurstCompiler.Options.EnableBurstSafetyChecks; BurstCompiler.Options.EnableBurstSafetyChecks = false;
            previousVsync = QualitySettings.vSyncCount; QualitySettings.vSyncCount = 0; previousTargetRate = Application.targetFrameRate; Application.targetFrameRate = -1;
            previousPipeline = QualitySettings.renderPipeline;
            pipeline = Own(Object.Instantiate((UniversalRenderPipelineAsset)(previousPipeline ? previousPipeline : GraphicsSettings.defaultRenderPipeline)));
            pipeline.shadowDistance = 300; pipeline.shadowCascadeCount = 4; pipeline.msaaSampleCount = 1; pipeline.renderScale = 1;
            QualitySettings.renderPipeline = pipeline;
            previousAmbientMode = RenderSettings.ambientMode; previousAmbient = RenderSettings.ambientLight; previousReflection = RenderSettings.reflectionIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black; RenderSettings.reflectionIntensity = 0;
            reference = Own(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/_Placeholder/Materials/BrickPlaceholder.mat")));
            // Pool/registered parity is checked on bare layers; QA-001's NOCTURNE plating rows (2/3) stay zero here.
            var layerParams = Own(new Texture2D(64, 4, TextureFormat.RGBAHalf, false, true) { filterMode = FilterMode.Point });
            for (int x = 0; x < 64; x++) for (int y = 0; y < 4; y++) layerParams.SetPixel(x, y, y == 1 ? new Color(1, 1, 1, 0) : Color.clear);
            layerParams.Apply(); reference.SetTexture("_BrickLayerParams", layerParams);
            indirect = Own(new Material(reference) { shader = Shader.Find("Game/Voxel/BrickLitIndirect") }); Assert.IsNotNull(indirect.shader);
            copy = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Game/Voxel/Rendering/Shaders/BrickPoolCopy.compute");
            target = Own(new RenderTexture(perf ? 1920 : 384, perf ? 1080 : 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); target.Create();
            camera = Own(new GameObject("Pooled brick gate camera")).AddComponent<Camera>(); camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.orthographic = true;
            camera.orthographicSize = perf ? 205 : 24; camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.transform.position = perf ? new Vector3(200, 200, -200) : new Vector3(8, 8, -40);
            var additional = camera.GetUniversalAdditionalCameraData(); additional.renderPostProcessing = false; additional.antialiasing = AntialiasingMode.None;
            var light = key = Own(new GameObject("Pooled brick gate key")).AddComponent<Light>(); light.type = LightType.Directional;
            light.shadows = LightShadows.Hard; light.transform.rotation = Quaternion.Euler(25, 30, 0);
            pool = new BrickPoolRenderer(indirect, copy, perf ? count * 578 : 4096, perf ? count * 3072 : 6144, perf ? count : 4);
            draw = (context, view) =>
            {
                if (view != camera) return;
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                pool.AdvanceFrame(++frame);
                if (world != null)
                {
                    if (moving) world.EntityManager.SetComponentData(root, LocalTransform.FromPosition(new float3(math.sin(frame * .05f) * 2, 0, 0)));
                    world.GetExistingSystemManaged<InitializationSystemGroup>()?.Update();
                    world.GetExistingSystemManaged<TransformSystemGroup>().Update();
                    new TransformBridge { Matrices = pool.WorldMatrices, States = world.EntityManager.GetComponentTypeHandle<BrickRenderState>(true), Transforms = world.EntityManager.GetComponentTypeHandle<LocalToWorld>(true) }.ScheduleParallel(bridgeQuery, bridgeQuery.GetDependency()).Complete();
                }
                pool.Render(camera, 300);
                lastCpu = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            };
            yield return null; yield return null;
            foreach (var view in Camera.allCameras) if (view != camera && view.enabled) { pausedCameras.Add(view); view.enabled = false; }
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            if (draw != null) RenderPipelineManager.beginCameraRendering -= draw;
            if (world != null && world.IsCreated) world.Dispose(); world = null;
            pool?.Dispose(); pool = null;
            QualitySettings.renderPipeline = previousPipeline; ShaderUtil.allowAsyncCompilation = previousAsync;
            BurstCompiler.Options.EnableBurstSafetyChecks = previousSafety; QualitySettings.vSyncCount = previousVsync; Application.targetFrameRate = previousTargetRate;
            RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientLight = previousAmbient; RenderSettings.reflectionIntensity = previousReflection;
            if (target) target.Release();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.Destroy(owned[i]);
            foreach (var view in pausedCameras) if (view) view.enabled = true; pausedCameras.Clear();
            owned.Clear(); initialized = false; frame = 0; yield return null;
        }
        [BurstCompile(CompileSynchronously = true)]
        struct TransformBridge : IJobChunk
        {
            // Each queried brick owns one distinct generational pool slot.
            [NativeDisableParallelForRestriction] public NativeArray<float4x4> Matrices;
            [ReadOnly] public ComponentTypeHandle<BrickRenderState> States;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> Transforms;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var states = chunk.GetNativeArray(ref States); var transforms = chunk.GetNativeArray(ref Transforms);
                var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (iterator.NextEntityIndex(out int i)) Matrices[states[i].Handle.Slot] = transforms[i].Value;
            }
        }
        static void Fill(NativeArray<PackedBrickVertex> vertices, NativeArray<uint> indices, int colour)
        {
            for (int side = 0; side < 2; side++)
            {
                for (int y = 0; y < 17; y++) for (int x = 0; x < 17; x++) vertices[side * 289 + y * 17 + x] = PackedBrickVertex.Pack(x * 2, y * 2, side * 32, side == 0 ? 5 : 4, 0, colour, 3, 0, false);
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    uint a = (uint)(side * 289 + y * 17 + x), b = a + 1, c = a + 18, d = a + 17; int k = (side * 256 + y * 16 + x) * 6;
                    indices[k] = a; indices[k + 1] = side == 0 ? d : b; indices[k + 2] = c;
                    indices[k + 3] = a; indices[k + 4] = c; indices[k + 5] = side == 0 ? b : d;
                }
            }
        }
        Mesh ReferenceMesh(NativeArray<PackedBrickVertex> vertices, NativeArray<uint> indices)
        {
            var mesh = Own(new Mesh());
            mesh.SetVertexBufferParams(vertices.Length, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.UNorm8, 4), new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm8, 4));
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length); mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32); mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
            mesh.subMeshCount = 1; mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length) { vertexCount = vertices.Length }, MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = new Bounds(Vector3.one * 8, Vector3.one * 16); return mesh;
        }
        Color32[] Pixels(string label)
        {
            var old = RenderTexture.active; RenderTexture.active = target;
            var t = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            try { t.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); t.Apply(); Directory.CreateDirectory("Logs/rendering"); File.WriteAllBytes("Logs/rendering/ren003-" + label + ".png", t.EncodeToPNG()); return t.GetPixels32(); }
            finally { Object.DestroyImmediate(t); RenderTexture.active = old; }
        }
        static void Match(Color32[] expected, Color32[] actual, string label)
        {
            long difference = 0, lit = 0;
            for (int i = 0; i < expected.Length; i++)
            { difference += Math.Abs(expected[i].r - actual[i].r) + Math.Abs(expected[i].g - actual[i].g) + Math.Abs(expected[i].b - actual[i].b); if (expected[i].r + expected[i].g + expected[i].b > 30) lit++; }
            double mae = difference / (expected.Length * 3.0); TestContext.WriteLine(label + " MAE=" + mae + "/255");
            Assert.Greater(lit, 1000, "A black reference cannot pass"); Assert.LessOrEqual(mae, 2, label);
        }
        [UnityTest] public IEnumerator Render_MatchesReferenceScreenshot()
        {
            yield return Setup(3, false);
            using var vertices = new NativeArray<PackedBrickVertex>(578, Allocator.Persistent);
            using var indices = new NativeArray<uint>(3072, Allocator.Persistent);
            var handles = new BrickPoolHandle[3]; var referenceObjects = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                Fill(vertices, indices, i + 2); float3 position = new float3((i - 1) * 20, 0, 0);
                handles[i] = pool.Upload(default, vertices, indices, new AABB { Center = 8, Extents = 8 }, float4x4.Translate(position));
                var go = Own(new GameObject("Registered shader reference", typeof(MeshFilter), typeof(MeshRenderer))); referenceObjects[i] = go;
                go.transform.position = position; go.GetComponent<MeshFilter>().sharedMesh = ReferenceMesh(vertices, indices); go.GetComponent<MeshRenderer>().sharedMaterial = reference;
            }
            yield return null; yield return null; var expected = Pixels("reference");
            foreach (var go in referenceObjects) go.SetActive(false);
            RenderPipelineManager.beginCameraRendering += draw;
            yield return null; yield return null; Match(expected, Pixels("pooled"), "Three pooled bricks");
            Assert.AreEqual(3, pool.VisibleCount); Assert.AreEqual(0, pool.MatrixUploadCount, "Idle matrices must not upload again");
            long beforeGrowth = pool.GpuBytes; pool.Reserve(pool.VertexCapacity + 1, pool.IndexCapacity + 1, 8);
            Assert.Greater(pool.GpuBytes, beforeGrowth, "Old buffers stay counted while retired");
            yield return null; yield return null; Match(expected, Pixels("grown"), "GPU growth copy");
            long pendingBytes = pool.GpuBytes;
            yield return null; yield return null; Assert.Less(pool.GpuBytes, pendingBytes, "Three-frame retired buffers released");
            pool.Release(handles[0]); Assert.Throws<InvalidOperationException>(() => pool.SetVisible(handles[0], true));
            Assert.Greater(pool.RetiredRangeCount, 0);
            yield return null; yield return null; Assert.Greater(pool.RetiredRangeCount, 0);
            yield return null; yield return null; Assert.AreEqual(0, pool.RetiredRangeCount);
            Fill(vertices, indices, 2); var replacement = pool.Upload(default, vertices, indices, new AABB { Center = 8, Extents = 8 }, float4x4.Translate(new float3(-20, 0, 0)));
            Assert.AreEqual(handles[0].Slot, replacement.Slot); Assert.AreNotEqual(handles[0].Generation, replacement.Generation);
            yield return null; yield return null; Match(expected, Pixels("reused"), "Retired slot reuse");
            BrickShaderTests.CompileVariants(indirect.shader, false);
            handles[0] = replacement;
            RenderPipelineManager.beginCameraRendering -= draw;
            for (int i = 0; i < 3; i++)
            {
                Fill(vertices, indices, i + 2);
                var writableVertices = vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    var packed = vertices[v];
                    packed.Hi = (packed.Hi & 0x00ffffffu) | ((uint)((v % 4) | (i << 2) | (i == 2 ? 16 : 0)) << 24);
                    writableVertices[v] = packed;
                }
                float3 position = new float3(-12 + i * 10, i * 2, i * 7);
                quaternion rotation = quaternion.RotateZ(i * .17f);
                pool.Upload(handles[i], vertices, indices, new AABB { Center = 8, Extents = 8 }, float4x4.TRS(position, rotation, new float3(.9f)));
                var go = referenceObjects[i]; go.transform.SetPositionAndRotation(position, rotation); go.transform.localScale = Vector3.one * .9f;
                go.GetComponent<MeshFilter>().sharedMesh = ReferenceMesh(vertices, indices); go.SetActive(true);
            }
            yield return null; yield return null; expected = Pixels("surface-shadow-reference");
            key.shadows = LightShadows.None; yield return null; yield return null; var clear = Pixels("surface-no-shadow");
            int darker = 0;
            for (int i = 0; i < clear.Length; i++) if (clear[i].r + clear[i].g + clear[i].b - expected[i].r - expected[i].g - expected[i].b > 12) darker++;
            Assert.Greater(darker, 50, "The parity scene must contain real visible shadows"); TestContext.WriteLine("Shadow parity receiver pixels=" + darker);
            key.shadows = LightShadows.Hard;
            foreach (var go in referenceObjects) go.SetActive(false);
            RenderPipelineManager.beginCameraRendering += draw;
            yield return null; yield return null; Match(expected, Pixels("surface-shadow-pooled"), "Rotated AO/damage/scorch/shadow parity");
            pool.SetTransform(replacement, float4x4.Translate(new float3(5000))); yield return null; yield return null; Assert.AreEqual(2, pool.VisibleCount, "Frustum and shadow distance both reject a distant brick");
            pool.SetTransform(replacement, float4x4.Translate(new float3(60, 0, 0))); yield return null; yield return null; Assert.AreEqual(3, pool.VisibleCount, "Off-frustum nearby shadow caster retained");
            RenderPipelineManager.beginCameraRendering -= draw;
            pool.Dispose(); pool = new BrickPoolRenderer(indirect, copy);
            ReferenceMemory();
        }
        [BurstCompile(CompileSynchronously = true)]
        struct MeshReferenceBrick : IJob
        {
            public VoxelStructureData Source;
            public Unity.Entities.BlobAssetReference<BlockDefsBlob> Blocks;
            public Unity.Entities.BlobAssetReference<OrientationTablesBlob> Orientations;
            public Unity.Entities.BlobAssetReference<BrickShapeTables> Shapes;
            public int3 Coord;
            public NativeArray<uint> Snapshot;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public void Execute()
            {
                BrickSnapshotUtility.FromStructure(Source, Coord, ref Blocks.Value, ref Orientations.Value, Snapshot);
                BrickMesher.MeshLod0(Snapshot, ref Shapes.Value, Vertices, Indices, out _, out _);
            }
        }
        void ReferenceMemory()
        {
            using var content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            ushort hull = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            using var source = BenchmarkStructures.HollowStation1M(hull);
            using var shapes = BrickShapeTablesBuilder.Build(ref content.Orientations.Value);
            using var snapshot = new NativeArray<uint>(MeshCell.SnapCells, Allocator.TempJob);
            using var vertices = new NativeList<PackedBrickVertex>(4096, Allocator.TempJob);
            using var indices = new NativeList<uint>(6144, Allocator.TempJob);
            long vertexCount = 0, indexCount = 0;
            for (int i = 0; i < source.Data.Bricks.Length; i++)
            {
                int3 coord = source.Data.Bricks[i].Coord;
                new MeshReferenceBrick { Source = source.Data, Blocks = content.BlockDefs, Orientations = content.Orientations, Shapes = shapes, Coord = coord, Snapshot = snapshot, Vertices = vertices, Indices = indices }.Schedule().Complete();
                vertexCount += vertices.Length; indexCount += indices.Length;
                pool.Upload(default, vertices.AsArray(), indices.AsArray(), new AABB { Center = 8, Extents = 8 }, float4x4.TRS((float3)(coord * 16) * .5f, quaternion.identity, new float3(.5f)));
            }
            long total = 10998784 + pool.ReservedBytes;
            TestContext.WriteLine($"Reference station: {source.Data.Bricks.Length} bricks, {vertexCount} vertices, {indexCount} indices; GPU={pool.GpuBytes}, native={pool.NativeBytes}, accounted total={total} B (integration remains TST-004)");
            if (total > 36000000) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Renderer accounted footprint {total} B; target <= 36000000 B"); Assert.AreEqual(872, pool.LiveCount);
        }
        static double Median(List<double> data) { data.Sort(); return data[data.Count / 2]; }
        [UnityTest, Timeout(600000)] public IEnumerator Idle2500_UnderBudget()
        {
            yield return Setup(2500, true);
            world = new World("REN003 transform gate");
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default).Where(t => t.ToString().StartsWith("Unity.Transforms.")).ToArray();
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            var em = world.EntityManager; root = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld)); em.SetComponentData(root, LocalTransform.Identity);
            using var vertices = new NativeArray<PackedBrickVertex>(578, Allocator.Persistent);
            using var indices = new NativeArray<uint>(3072, Allocator.Persistent); Fill(vertices, indices, 2);
            var handles = new BrickPoolHandle[2500];
            var archetype = em.CreateArchetype(typeof(LocalTransform), typeof(LocalToWorld), typeof(Parent), typeof(BrickRenderState));
            using (var entities = em.CreateEntity(archetype, 2500, Allocator.Persistent))
            for (int i = 0; i < 2500; i++)
            {
                float3 position = new float3((i % 50) * 8, (i / 50) * 8, 0);
                handles[i] = pool.Upload(default, vertices, indices, new AABB { Center = 8, Extents = 8 }, float4x4.TRS(position, quaternion.identity, new float3(.5f)));
                em.SetComponentData(entities[i], LocalTransform.FromPositionRotationScale(position, quaternion.identity, .5f));
                em.SetComponentData(entities[i], new Parent { Value = root }); em.SetComponentData(entities[i], new BrickRenderState { Handle = handles[i], HasMesh = 1 });
                if ((i + 1) % 64 == 0) yield return null;
            }
            GameLog.Info("Test", "REN-003 setup uploaded 2500 bricks");
            bridgeQuery = em.CreateEntityQuery(typeof(BrickRenderState), typeof(LocalToWorld));
            RenderPipelineManager.beginCameraRendering += draw;
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual(2500, pool.VisibleCount); Assert.AreEqual(RenderingThreadingMode.MultiThreaded, SystemInfo.renderingThreadingMode);
            Assert.IsTrue(FrameTimingManager.IsFeatureEnabled(), "Enable frame timing before launching the Editor; never pass absent GPU timings");
            using var renderLoop = ProfilerRecorder.StartNew(ProfilerCategory.Render, "RenderLoop", 1);
            var cpu = new List<double>(); var gpu = new List<double>(); var render = new List<double>();
            var timing = new FrameTiming[1]; ulong stamp = 0; double deadline = Time.realtimeSinceStartupAsDouble + 600;
            for (int i = 0; i < 300 || gpu.Count < 300; i++)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "300 unique positive FrameTiming GPU samples required for diagnostic comparison");
                FrameTimingManager.CaptureFrameTimings(); yield return null;
                if (i < 300) { cpu.Add(lastCpu); render.Add(renderLoop.LastValue / 1e6); }
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0 && timing[0].frameStartTimestamp != stamp)
                { stamp = timing[0].frameStartTimestamp; gpu.Add(timing[0].gpuFrameTime); }
            }
            double idle = Report("Idle inclusive CPU", cpu); Report("FrameTiming GPU diagnostic (not native submission span)", gpu);
            Report("RenderLoop active work", render); Assert.IsTrue(renderLoop.Valid); Assert.Greater(Median(render), 0);
            if (idle > .6) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Idle inclusive CPU {idle:F6} ms; target <= 0.6 ms"); Assert.AreEqual(2500, pool.VisibleCount); Assert.AreEqual(1, pool.DrawCount, "Adjacent selected index ranges coalesce without removing geometry");
            moving = true; for (int i = 0; i < 60; i++) yield return null;
            cpu.Clear(); for (int i = 0; i < 300; i++) { yield return null; cpu.Add(lastCpu); }
            double delta = Report("Moving inclusive CPU", cpu) - idle; TestContext.WriteLine($"Moving delta {delta:F6} ms"); if (delta > .5) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Moving inclusive CPU delta {delta:F6} ms; target <= 0.5 ms");
            moving = false;
            var apply = new List<double>();
            for (int iteration = 0; iteration < 300; iteration++)
            {
                Fill(vertices, indices, 2 + iteration % 2);
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < 128; i++) pool.Upload(handles[i], vertices, indices, new AABB { Center = 8, Extents = 8 }, pool.WorldMatrices[handles[i].Slot]);
                apply.Add((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency); yield return null;
            }
            double rewrite = Report("Rewrite128", apply); if (rewrite > 2.5) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Rewrite128 {rewrite:F6} ms; target <= 2.5 ms");
            camera.orthographicSize = 5; camera.transform.position = new Vector3(4, 4, -20); yield return null; yield return null;
            var oldPixels = Pixels("before-rewrite"); Fill(vertices, indices, 6);
            for (int i = 0; i < 128; i++) pool.Upload(handles[i], vertices, indices, new AABB { Center = 8, Extents = 8 }, pool.WorldMatrices[handles[i].Slot]);
            yield return null; yield return null; var newPixels = Pixels("after-rewrite");
            Assert.IsTrue(oldPixels.Where((c, i) => c.r != newPixels[i].r || c.g != newPixels[i].g || c.b != newPixels[i].b).Any(), "Rewrite must change visible pixels");
            camera.orthographicSize = 205; camera.transform.position = new Vector3(200, 200, -200);
            em.SetComponentData(root, LocalTransform.Identity);
            for (int i = 0; i < 60; i++) yield return null;
            var nativeGpu = new List<double>(); var nativeSpans = new List<double>();
            yield return MetalGpuTimingCapture.Capture(nativeGpu, nativeSpans);
            double nativeMedian = Report("Native GPU execution (all upload/render intervals, overlap counted once)", nativeGpu);
            Report("Native GPU elapsed upload-through-render span (includes idle gaps)", nativeSpans);
            var threadTimes = new List<double>(); yield return CaptureRenderThread(threadTimes);
            double renderThread = Report("Full render-thread root", threadTimes); if (renderThread > 6) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Full render-thread root {renderThread:F6} ms; target <= 6 ms");
            if (nativeMedian > 6) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Native render median {nativeMedian:F6} ms; target <= 6 ms");
        }
        static double Report(string name, List<double> samples)
        {
            Assert.Greater(samples.Count, 0); double median = Median(samples), p95 = samples[(int)Math.Floor((samples.Count - 1) * .95)];
            TestContext.WriteLine($"{name}: n={samples.Count}, median={median:F6} ms, p95={p95:F6} ms"); return median;
        }
        static IEnumerator CaptureRenderThread(List<double> samples)
        {
            bool enabled = ProfilerDriver.enabled, editor = ProfilerDriver.profileEditor, cpu = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU);
            try
            {
                ProfilerDriver.profileEditor = false; ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true); ProfilerDriver.enabled = true;
                for (int i = 0; i < 20; i++) yield return null;
                int threadIndex = -1, frameIndex = ProfilerDriver.lastFrameIndex;
                for (int i = 0; i < 256; i++)
                {
                    using var view = ProfilerDriver.GetRawFrameDataView(frameIndex, i);
                    if (!view.valid) break;
                    if (view.threadName == "Render Thread") { threadIndex = i; break; }
                }
                Assert.GreaterOrEqual(threadIndex, 0, "A real render thread is required; RenderLoop is not a substitute");
                double deadline = Time.realtimeSinceStartupAsDouble + 90;
                while (samples.Count < 300)
                {
                    Assert.Less(Time.realtimeSinceStartupAsDouble, deadline); yield return null;
                    int current = ProfilerDriver.lastFrameIndex; if (current == frameIndex) continue; frameIndex = current;
                    using var view = ProfilerDriver.GetRawFrameDataView(current, threadIndex);
                    Assert.IsTrue(view.valid); Assert.AreEqual("Render Thread", view.threadName); Assert.Greater(view.sampleCount, 0);
                    Assert.AreEqual(view.sampleCount - 1, view.GetSampleChildrenCountRecursive(0), "Root must include the entire thread frame");
                    double duration = view.GetSampleTimeMs(0); Assert.Greater(duration, 0); samples.Add(duration);
                }
            }
            finally { ProfilerDriver.enabled = enabled; ProfilerDriver.profileEditor = editor; ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, cpu); }
        }
    }
}
