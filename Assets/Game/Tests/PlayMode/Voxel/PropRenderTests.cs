using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.Common.Perf;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Voxel
{
    public sealed class PropRenderTests
    {
        // Test-only equivalent of the EditMode factory, without a test-assembly dependency or content asset.
        static class TestContentFactory
        {
            public static BlockDefinition Prop(Mesh mesh)
            {
                var block = ScriptableObject.CreateInstance<BlockDefinition>();
                var serialized = new SerializedObject(block);
                serialized.FindProperty("id").stringValue = "block_test_prop";
                serialized.FindProperty("kind").intValue = (int)BlockKind.Prop;
                serialized.FindProperty("propMesh").objectReferenceValue = mesh;
                serialized.ApplyModifiedPropertiesWithoutUndo(); return block;
            }
        }
        sealed class Fixture : IDisposable
        {
            public readonly World World;
            readonly ContentService content;
            readonly VoxelRenderConfig config;
            readonly BlobAssetReference<BlockDefsBlob> blocks;
            readonly BlobAssetReference<BlockModulesBlob> modules;
            readonly BlockDefinition definition;
            readonly Mesh mesh;
            readonly Texture2D propMask, propNormal;
            readonly SystemHandle apply, render;
            readonly bool oldAsync;
            public readonly Entity State;
            public readonly Camera Camera;
            public readonly RenderTexture Target;
            public readonly Entity[] Roots, Bricks;
            public EntityManager EM => World.EntityManager;
            public VoxelRenderManagedState Managed => EM.GetComponentData<VoxelRenderRuntime>(State).Managed.Value;
            public int Count => Managed.PropCounts.Count == 0 ? 0 : Managed.PropCounts[0];
            public Fixture(int gridCount = 1, int propsPerGrid = 1)
            {
                oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
                mesh = new Mesh { name = "Test prop quad" };
                mesh.vertices = new[] { new Vector3(-.4f,-.4f,0), new Vector3(.4f,-.4f,0), new Vector3(.4f,.4f,0), new Vector3(-.4f,.4f,0) };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.colors = new[] { Color.red, Color.red, Color.red, Color.red }; mesh.triangles = new[] { 0,3,2,0,2,1 }; mesh.RecalculateBounds();
                definition = TestContentFactory.Prop(mesh); var report = new ValidationReport("prop test");
                var catalog = ContentCatalog.Build(new ContentDefinition[] { definition }, report);
                var meshes = new List<Mesh>(); blocks = BlockDefsBlobBuilder.Build(catalog, report, meshes, Allocator.Persistent, out modules);
                content = new ContentService(Resources.Load<Game.App.Boot.GameRoot>("GameRoot").Config.ContentDatabase);
                World = new World("PropRenderTests"); World.GetOrCreateSystemManaged<PresentationSystemGroup>();
                World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
                apply = World.CreateSystem<BrickMeshApplySystem>(); render = World.CreateSystem<PropRenderSystem>();
                State = VoxelSingletons.CreateAll(EM); content.CreateSingletons(EM);
                using (var q = EM.CreateEntityQuery(typeof(BlockDefsRef))) EM.SetComponentData(q.GetSingletonEntity(), new BlockDefsRef { Value = blocks });
                config = VoxelRenderConfigFactory.Create(content.Catalog.Get<VoxelRenderConfigDefinition>(new ContentId("config_voxel_render")), meshes);
                EM.CreateSingleton(config); apply.Update(World.Unmanaged);
                Camera = new GameObject("Prop test camera").AddComponent<Camera>();
                Camera.transform.position = new Vector3(.5f,.5f,-8); Camera.orthographic = true; Camera.orthographicSize = 3; Camera.aspect = 1;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.black;
                Target = new RenderTexture(256,256,24); Target.Create(); Camera.targetTexture = Target;
                EM.SetComponentData(State, new VoxelRenderCamera { Value = Camera });
                // Emission makes the pixel assertion independent of scene lighting; production shader/instancing is unchanged.
                propMask = new Texture2D(1,1,TextureFormat.RGBA32,false,true); propMask.SetPixel(0,0,new Color(0,.4f,1,1)); propMask.Apply();
                propNormal = new Texture2D(1,1,TextureFormat.RGBA32,false,true); propNormal.SetPixel(0,0,new Color(.5f,.5f,1,1)); propNormal.Apply();
                Managed.PropMaterial.SetTexture("_PropMask", propMask); Managed.PropMaterial.SetTexture("_PropNormal", propNormal);
                Managed.PropMaterial.SetColor("_TeamAccent", Color.red); Managed.PropMaterial.SetFloat("_EmissiveIntensity", 1);
                Managed.PropMaterial.SetFloat("_Powered", 1);
                Roots = new Entity[gridCount]; Bricks = new Entity[gridCount];
                for (int i = 0; i < gridCount; i++)
                {
                    Roots[i] = EM.CreateEntity(typeof(VoxelGrid), typeof(LocalToWorld), typeof(BrickSlot), typeof(PaletteEntry), typeof(BlockInstance));
                    EM.SetComponentData(Roots[i], new VoxelGrid { CellSize = 1, BrickDims = new int3(1) });
                    EM.SetComponentData(Roots[i], new LocalToWorld { Value = float4x4.Translate(new float3(i * 20,0,0)) });
                    Bricks[i] = EM.CreateEntity(typeof(Brick), typeof(BrickRenderState), typeof(BrickPropInstance), typeof(LocalToWorld), typeof(BrickCells), typeof(BrickSparseCells));
                    EM.SetComponentData(Bricks[i], new Brick { Grid = Roots[i] });
                    EM.SetComponentData(Bricks[i], new LocalToWorld { Value = float4x4.identity });
                    EM.GetBuffer<BrickSlot>(Roots[i]).Add(new BrickSlot { Brick = Bricks[i] });
                    var palette = EM.GetBuffer<PaletteEntry>(Roots[i]); palette.Add(default); palette.Add(new PaletteEntry { Block = 1, Color = 2 });
                    ApplyProps(i, propsPerGrid);
                }
                RenderPipelineManager.beginCameraRendering += Draw;
            }
            void Draw(ScriptableRenderContext context, Camera camera) { if (camera == Camera) Update(); }
            public void Update() => render.Update(World.Unmanaged);
            public void ApplyProps(int brick, int count)
            {
                var batch = EM.GetComponentData<BrickMeshBatch>(State); batch.Work.Clear();
                batch.Work.Add(new BrickMeshWork { Brick = Bricks[brick], Grid = Roots[brick] });
                // Real cells -> production snapshot -> meshing -> ApplyResultsJob -> prop renderer.
                var cells = EM.GetBuffer<BrickCells>(Bricks[brick]); cells.ResizeUninitialized(4096);
                for (int i = 0; i < cells.Length; i++) cells[i] = new BrickCells { Value = i < count ? Cell.Palette(1) : default };
                using var orientations = EM.CreateEntityQuery(typeof(OrientationTablesRef));
                ref var state = ref World.Unmanaged.ResolveSystemStateRef(apply);
                new BrickSnapshotJob { Bricks = state.GetComponentLookup<Brick>(true), Grids = state.GetComponentLookup<VoxelGrid>(true),
                    Slots = state.GetBufferLookup<BrickSlot>(true), Cells = state.GetBufferLookup<BrickCells>(true), Sparse = state.GetBufferLookup<BrickSparseCells>(true),
                    Palettes = state.GetBufferLookup<PaletteEntry>(true), Instances = state.GetBufferLookup<BlockInstance>(true), Blocks = blocks,
                    Orientations = orientations.GetSingleton<OrientationTablesRef>().Value, Work = batch.Work.AsArray(), Snapshots = batch.Snapshots, Props = batch.Props }.Schedule(1,1).Complete();
                batch.Handle = new BrickMeshJob { Work = batch.Work.AsArray(), Results = batch.Results, Snapshots = batch.Snapshots,
                    Shapes = EM.GetComponentData<VoxelRenderRuntime>(State).Shapes }.Schedule(1,1);
                batch.Handle.Complete(); batch.InFlight = 1; EM.SetComponentData(State, batch); apply.Update(World.Unmanaged);
            }
            public int ColoredPixels;
            public Color Pixel(string name)
            {
                var previous = RenderTexture.active; RenderTexture.active = Target;
                var pixels = new Texture2D(256,256,TextureFormat.RGBA32,false);
                try
                {
                    pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                    ColoredPixels = 0; foreach (var pixel in pixels.GetPixels32()) if (pixel.r > 64) ColoredPixels++;
                    if (name != null) { Directory.CreateDirectory("Logs/playtest/ren014"); File.WriteAllBytes("Logs/playtest/ren014/" + name + ".png", pixels.EncodeToPNG()); }
                    return pixels.GetPixel(128,128);
                }
                finally { Object.DestroyImmediate(pixels); RenderTexture.active = previous; }
            }
            public void Dispose()
            {
                RenderPipelineManager.beginCameraRendering -= Draw;
                VoxelSingletons.DisposeAll(EM, State); World.Dispose();
                var owned = config; VoxelRenderConfigFactory.Destroy(ref owned); content.Dispose(); blocks.Dispose(); modules.Dispose();
                Camera.targetTexture = null; Target.Release(); Object.DestroyImmediate(Target); Object.DestroyImmediate(Camera.gameObject);
                Object.DestroyImmediate(propMask); Object.DestroyImmediate(propNormal);
                Object.DestroyImmediate(mesh); Object.DestroyImmediate(definition); ShaderUtil.allowAsyncCompilation = oldAsync;
            }
        }
        [SetUp] public void RequireGpu()
        { if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Prop rendering requires a real GPU."); }
        [UnityTest] public IEnumerator Props_Render()
        {
            using var f = new Fixture(); yield return Rendered(f, true);
            Assert.AreEqual(1, f.Count); Assert.Greater(f.Pixel("prop-visible").r, .2f);
            Assert.Greater(f.ColoredPixels, 800, "The whole quad must render, not only its centre pixel");
            var buffer = f.Managed.PropInstanceBuffers[0];
            // Move well outside initial bounds, then follow with the camera: no cell upload should be required.
            f.EM.SetComponentData(f.Roots[0], new LocalToWorld { Value = float4x4.Translate(new float3(100,0,0)) });
            f.Camera.transform.position += Vector3.right * 100;
            yield return Rendered(f, true);
            Assert.Greater(f.Pixel("prop-moved").r, .2f); Assert.AreSame(buffer, f.Managed.PropInstanceBuffers[0]);
        }
        [UnityTest] public IEnumerator PropRemoved_Disappears()
        {
            using var f = new Fixture(); yield return Rendered(f, true);
            Assert.Greater(f.Pixel("before-removal").r, .2f);
            f.ApplyProps(0,0); yield return Rendered(f, false);
            Assert.AreEqual(0,f.Count); Assert.Less(f.Pixel("after-removal").maxColorComponent,.05f);
            f.ApplyProps(0,1); yield return Rendered(f, true); Assert.AreEqual(1,f.Count);
            f.EM.DestroyEntity(f.Bricks[0]); yield return null; yield return null; Assert.AreEqual(0,f.Count);
        }
        [UnityTest, Category("Performance")] public IEnumerator Budget10000()
        {
            using var f = new Fixture(20,500); var steady = new double[60]; var rebuild = new double[60];
            for (int i = -10; i < 60; i++)
            {
                for (int g = 0; g < f.Roots.Length; g++) f.EM.SetComponentData(f.Roots[g], new LocalToWorld { Value = float4x4.Translate(new float3(g*20,0,i*.1f)) });
                long start = Stopwatch.GetTimestamp(); f.Update(); double ms = (Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                if (i >= 0) steady[i] = ms;
                f.ApplyProps(0,500); start = Stopwatch.GetTimestamp(); f.Update(); ms = (Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                if (i >= 0) rebuild[i] = ms;
                Assert.AreEqual(10000,f.Count); yield return null;
            }
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Props10000",steady,"ms").AddSamples("PropRebuild10000",rebuild,"ms");
            run.Write("Props10000",.3); run.Write("PropRebuild10000",1);
            run.AssertMedian("Props10000",.3); run.AssertMedian("PropRebuild10000",1);
        }
        static IEnumerator Rendered(Fixture fixture, bool visible)
        {
            // A test coroutine can run before this camera's first render. Wait for GPU pixels, not a frame count.
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            yield return null;
            while ((fixture.Pixel(null).r > .2f) != visible && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(visible, fixture.Pixel(null).r > .2f, "Expected rendered prop visibility before timeout");
        }
        [UnityTest] public IEnumerator NoBufferLeaks()
        {
            var leaks = new List<string>();
            void OnLog(string text, string stack, LogType type)
            { if (text.Contains("GraphicsBuffer") && (text.Contains("leak") || text.Contains("released"))) leaks.Add(text); }
            Application.logMessageReceived += OnLog;
            try
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    var f = new Fixture(); f.Update(); var instances = f.Managed.PropInstanceBuffers[0]; var grids = f.Managed.GridMatrices;
                    f.Dispose(); Assert.IsFalse(instances.IsValid()); Assert.IsFalse(grids.IsValid()); yield return null;
                }
                Assert.IsEmpty(leaks);
            }
            finally { Application.logMessageReceived -= OnLog; }
        }
    }
}
