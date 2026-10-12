using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Tests.PlayMode.Voxel.Physics.Perf;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Voxel
{
    /// <summary>Real spawn, snapshot, mesher, apply and pooled renderer, driven once before the test camera renders.</summary>
    internal sealed class BrickRenderFixture : IDisposable
    {
        public readonly EcsTestWorld Test;
        public EntityManager Em => Test.EntityManager;
        public readonly Camera Camera;
        public readonly RenderTexture Target;
        public readonly Entity Root;
        public readonly int BrickCount;
        public int Frames { get; private set; }
        public IReadOnlyList<Type> SystemTypes { get; private set; }
        public long SourceBytes => source.NativeBytes;
        readonly int sourceHandle;
        public Action BeforeFrame;
        public Action AfterFrame;
        readonly ContentService content;
        readonly BlueprintLibrary library;
        readonly VoxelStructureRegistry registry;
        readonly VoxelStructure source;
        readonly VoxelRenderConfig renderConfig;
        readonly List<Camera> otherCameras = new List<Camera>();
        readonly List<Light> otherLights = new List<Light>();
        readonly Light light;
        readonly bool oldAsync;
        readonly AmbientMode oldAmbientMode;
        readonly Color oldAmbient;
        readonly float oldReflection;
        readonly RenderPipelineAsset oldPipeline;
        readonly UniversalRenderPipelineAsset pipeline;
        public BrickPoolRenderer Pool => Singleton<VoxelRenderRuntime>().Managed.Value.Pool;
        public T Singleton<T>() where T : unmanaged, IComponentData
        { using var q = Em.CreateEntityQuery(typeof(T)); return q.GetSingleton<T>(); }
        public BrickRenderFixture(string shape)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("REN-013 requires a GPU; run batch PlayMode without -nographics.");
            oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            oldPipeline = QualitySettings.renderPipeline;
            pipeline = Object.Instantiate((UniversalRenderPipelineAsset)(oldPipeline ? oldPipeline : GraphicsSettings.defaultRenderPipeline));
            pipeline.msaaSampleCount = 1; pipeline.renderScale = 1; QualitySettings.renderPipeline = pipeline;
            oldAmbientMode = RenderSettings.ambientMode; oldAmbient = RenderSettings.ambientLight; oldReflection = RenderSettings.reflectionIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.15f,.15f,.15f); RenderSettings.reflectionIntensity = 0;
            foreach (var view in Camera.allCameras) if (view.enabled) { otherCameras.Add(view); view.enabled = false; }
            foreach (var lamp in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (lamp.enabled) { otherLights.Add(lamp); lamp.enabled = false; }
            content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            library = new BlueprintLibrary(content.Catalog, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations });
            registry = VoxelStructureRegistry.CreateRuntime(library);
            var systems = new List<Type> { typeof(GridSpawnSystem), typeof(GridSpawnBodyInitSystem), typeof(GridBodyInitSystem), typeof(GridMassUpdateSystem),
                typeof(BrickDirtyScheduleSystem), typeof(ColliderSnapshotSystem), typeof(BrickColliderBuildSystem), typeof(ColliderSwapSystem),
                typeof(GridColliderCleanupSystem), typeof(DeferredDisposalSystem), typeof(GameplayEndCommandBufferSystem),
                typeof(BrickMeshBuildSystem), typeof(BrickMeshApplySystem), typeof(BrickRenderCleanupSystem), typeof(GridLodSystem), typeof(BrickPoolRenderSystem), typeof(PropRenderSystem) };
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics", StringComparison.Ordinal) || type.Namespace == "Unity.Transforms" || type.Namespace == "Unity.Rendering")) systems.Add(type);
            SystemTypes = systems;
            Test = EcsTestWorld.Create(systems.ToArray()); Test.AddGameplayActive(); Test.AddSimClock(); content.CreateSingletons(Em);
            Em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
            Em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f, RequireRenderer = 1 });
            var physics = content.Catalog.Get<PhysicsVoxelConfigDefinition>(new ContentId("config_physics"));
            Em.CreateSingleton(PhysicsVoxelConfigFactory.Create(physics)); Em.CreateSingleton(PhysicsVoxelConfigFactory.CreatePhysicsStep(physics));
            renderConfig = VoxelRenderConfigFactory.Create(content.Catalog.Get<VoxelRenderConfigDefinition>(new ContentId("config_voxel_render")), content.PropMeshes); Em.CreateSingleton(renderConfig);
            ushort hull = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            source = shape == "lattice" ? BenchmarkStructures.Lattice2500(hull) : shape == "station" ? BenchmarkStructures.HollowStation1M(hull) : shape == "thin" ? BenchmarkStructures.ThinShellStation1M(hull) : BenchmarkStructures.Ship20k(hull);
            BrickCount = shape == "lattice" ? 2500 : shape == "station" ? 872 : shape == "thin" ? 3784 : 10;
            sourceHandle = registry.Register(source);
            Root = GridSpawnApi.RequestSpawn(Em, registry, sourceHandle, new GridSpawnParams { Pose = RigidTransform.identity, Motion = GridMotion.Static, Flags = GridSpawnFlags.Transient });
            Camera = new GameObject("REN-013 camera").AddComponent<Camera>(); Camera.orthographic = true; Camera.aspect = 1920f/1080; Camera.nearClipPlane = .1f; Camera.farClipPlane = 1500;
            Vector3 center = shape == "lattice" ? new Vector3(100,16,100) : shape == "station" ? new Vector3(64,32,64) : shape == "thin" ? new Vector3(128,64,128) : new Vector3(20,2.5f,6.25f);
            Camera.orthographicSize = shape == "lattice" ? 145 : shape == "station" ? 100 : shape == "thin" ? 200 : 18;
            Camera.transform.position = center + (shape == "ship" ? new Vector3(42,30,-55) : new Vector3(130,110,-190)); Camera.transform.LookAt(center);
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.black;
            var extra = Camera.GetUniversalAdditionalCameraData(); extra.renderPostProcessing = false; extra.antialiasing = AntialiasingMode.None;
            Target = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear); Target.Create(); Camera.targetTexture = Target;
            light = new GameObject("REN-013 key").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; light.shadows = LightShadows.Hard; light.transform.rotation = Quaternion.Euler(35,-30,0);
            using (var q = Em.CreateEntityQuery(typeof(VoxelRenderCamera))) q.SetSingleton(new VoxelRenderCamera { Value = Camera });
            RenderPipelineManager.beginCameraRendering += Draw;
        }
        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (camera != Camera) return;
            BeforeFrame?.Invoke(); Test.StepTicks(1); Frames++; AfterFrame?.Invoke();
        }
        public bool IsReady(bool collision = false)
        {
            if (Frames == 0 || !Em.HasComponent<GridSpawning>(Root) || Em.IsComponentEnabled<GridSpawning>(Root)) return false;
            if (collision && (!Em.HasComponent<GridColliderState>(Root) || Em.GetComponentData<GridColliderState>(Root).Phase != GridColliderPhase.Ready ||
                !Em.HasComponent<Unity.Physics.PhysicsCollider>(Root) || !Em.GetComponentData<Unity.Physics.PhysicsCollider>(Root).IsValid)) return false;
            return BrickRenderStats.Gather(Em).Meshes == BrickCount && Singleton<BrickMeshBatch>().InFlight == 0 && Pool.VisibleCount > 0;
        }
        public void ReleaseSource() { registry.Release(sourceHandle); source.Dispose(); }
        public IEnumerator Ready(bool collision = false)
        {
            double until = Time.realtimeSinceStartupAsDouble + 60;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
                if (IsReady(collision)) yield break;
            }
            Assert.Fail("Spawn/mesh/apply did not become visible; frames=" + Frames);
        }
        public IEnumerator WaitFrames(int count)
        { int target = Frames + count; double until = Time.realtimeSinceStartupAsDouble + 30; while (Frames < target) { Assert.Less(Time.realtimeSinceStartupAsDouble, until, "Test camera stopped rendering"); yield return null; } }
        public Texture2D ReadPixels()
        {
            var previous = RenderTexture.active; RenderTexture.active = Target;
            try { var image = new Texture2D(Target.width,Target.height,TextureFormat.RGB24,false,true); image.ReadPixels(new Rect(0,0,Target.width,Target.height),0,0); image.Apply(); return image; }
            finally { RenderTexture.active = previous; }
        }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Draw; Test.Dispose(); Object.DestroyImmediate(registry); source.Dispose(); library.Dispose(); content.Dispose();
            var owned = renderConfig; VoxelRenderConfigFactory.Destroy(ref owned);
            Camera.targetTexture = null; Target.Release(); Object.DestroyImmediate(Target); Object.DestroyImmediate(Camera.gameObject); Object.DestroyImmediate(light.gameObject);
            QualitySettings.renderPipeline = oldPipeline; Object.DestroyImmediate(pipeline); ShaderUtil.allowAsyncCompilation = oldAsync;
            RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientLight = oldAmbient; RenderSettings.reflectionIntensity = oldReflection;
            foreach (var view in otherCameras) if (view) view.enabled = true;
            foreach (var lamp in otherLights) if (lamp) lamp.enabled = true;
        }
    }
    internal sealed class BrickRenderRecorder : IDisposable
    {
        readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        public BrickRenderRecorder(BrickRenderFixture fixture, bool moving, bool allVoxel = false)
        {
            var world = fixture.Test.World;
            var systems = new List<SystemHandle> { world.GetExistingSystemManaged<EntitiesGraphicsSystem>().SystemHandle, world.GetExistingSystem<BrickPoolRenderSystem>() };
            if (allVoxel)
            {
                systems.Clear(); systems.Add(world.GetExistingSystemManaged<EntitiesGraphicsSystem>().SystemHandle);
                foreach (var type in fixture.SystemTypes)
                    if (type.Namespace != null && type.Namespace.StartsWith("Game.Voxel", StringComparison.Ordinal) && !typeof(ComponentSystemGroup).IsAssignableFrom(type)) systems.Add(world.GetExistingSystem(type));
            }
            else if (moving) systems.Add(world.GetExistingSystemManaged<TransformSystemGroup>().SystemHandle);
            else systems.AddRange(new[] { world.GetExistingSystem<BrickMeshBuildSystem>(), world.GetExistingSystem<BrickMeshApplySystem>(), world.GetExistingSystem<BrickRenderCleanupSystem>(), world.GetExistingSystem<GridLodSystem>(), world.GetExistingSystem<PropRenderSystem>() });
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var system in systems)
            {
                string name = EntityManager.EntityManagerDebug.GetSystemProfilerMarkerName(world, system); int found = 0;
                foreach (var handle in handles)
                    if (ProfilerRecorderHandle.GetDescription(handle).Name == name)
                    {
                        var recorder = new ProfilerRecorder(handle,1,ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
                        Assert.IsTrue(recorder.Valid && recorder.IsRunning, name); recorders.Add(recorder); found++;
                    }
                Assert.Greater(found,0,name);
            }
        }
        public double CurrentMs { get { double ns = 0; foreach (var recorder in recorders) ns += recorder.CurrentValue; return ns * 1e-6; } }
        public void Dispose() { foreach (var recorder in recorders) recorder.Dispose(); }
    }
    public sealed class BrickRenderPerfTests
    {
        [UnityTest, Category("Performance")] public IEnumerator MovingLattice2500_TransformUnder0_5ms()
        {
            using var scope = new PhysicsPerfScope(); using var f = new BrickRenderFixture("lattice"); yield return f.Ready();
            using var recorder = new BrickRenderRecorder(f,true); double before = 0; bool moving = false;
            var samples = new List<double>();
            f.BeforeFrame = () => { if (moving) f.Em.SetComponentData(f.Root, LocalTransform.FromPosition(new float3(math.cos(f.Frames*.03f)*3,0,math.sin(f.Frames*.03f)*3))); before = recorder.CurrentMs; };
            f.AfterFrame = () => samples.Add(recorder.CurrentMs-before);
            yield return f.WaitFrames(60); samples.Clear(); yield return f.WaitFrames(300); var stationary = samples.ToArray();
            moving = true; yield return f.WaitFrames(60); samples.Clear(); yield return f.WaitFrames(300); var motion = samples.ToArray();
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Static",stationary).AddSamples("Moving",motion);
            double delta = run.Get("Moving").Median-run.Get("Static").Median;
            run.AddSamples("MovingDelta",new[] { delta }); run.Write("MovingDelta",.5);
            TestContext.WriteLine($"2500 applied bricks, static {run.Get("Static").Median:F6}, moving {run.Get("Moving").Median:F6}, delta {delta:F6} ms; pooled renderer + transforms + Entities Graphics");
            Assert.Greater(run.Get("Static").Median,0); Assert.Greater(run.Get("Moving").Median,0); run.AssertMedian("MovingDelta",.5);
        }
        [UnityTest, Category("Performance")] public IEnumerator IdleStation1M_RenderShare()
        {
            using var scope = new PhysicsPerfScope(); using var f = new BrickRenderFixture("station"); yield return f.Ready();
            using var recorder = new BrickRenderRecorder(f,false); double before = 0; var samples = new List<double>();
            f.BeforeFrame = () => before = recorder.CurrentMs; f.AfterFrame = () => samples.Add(recorder.CurrentMs-before);
            yield return f.WaitFrames(60); samples.Clear(); yield return f.WaitFrames(300);
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("RenderShare",samples); run.Write("RenderShare",1.2);
            TestContext.WriteLine($"872 applied bricks / 1,008,128 cells: render share {run.Get("RenderShare").Median:F6} / {run.Get("RenderShare").P95:F6} ms median/p95");
            Assert.Greater(run.Get("RenderShare").Median,0); run.AssertMedian("RenderShare",1.2);
        }
    }
}
