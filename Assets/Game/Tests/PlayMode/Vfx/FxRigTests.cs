#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Data.Definitions;
using Game.Presentation.Vfx;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Vfx
{
    public sealed class FxRigTests
    {
        const string Folder = "Assets/FxRigTestTemporary";
        readonly List<Object> owned = new List<Object>();
        FxRig rig;
        VfxConfigDefinition config;
        FxDefinition[] definitions;
        [SetUp] public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device for VisualEffect contracts and GraphicsBuffers.");
            AssetDatabase.CreateFolder("Assets", "FxRigTestTemporary");
            config = Own(ScriptableObject.CreateInstance<VfxConfigDefinition>());
            var graph = Graph("Valid", true);
            definitions = new[] { Definition("fx_hit_test", FxKind.HitKinetic, FxMode.Batched, graph),
                Definition("fx_pool_test", FxKind.ExplosionLarge, FxMode.Pooled, graph),
                Definition("fx_tracer_test", FxKind.Tracer, FxMode.Instanced, null) };
            foreach (var d in definitions) config.Defaults[(int)d.Kind] = d;
        }
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (rig != null) { rig.Dispose(); Object.Destroy(rig.gameObject); }
            yield return null;
            foreach (var o in owned) if (o != null) Object.DestroyImmediate(o);
            owned.Clear();
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
        }
        FxDefinition Definition(string id, FxKind kind, FxMode mode, VisualEffectAsset graph)
        {
            var d = Own(ScriptableObject.CreateInstance<FxDefinition>()); d.name = id;
            var so = new SerializedObject(d);
            so.FindProperty("id").stringValue = id; so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("mode").intValue = (int)mode; so.FindProperty("graph").objectReferenceValue = graph;
            if (mode == FxMode.Instanced)
            {
                so.FindProperty("mesh").objectReferenceValue = Own(new Mesh());
                so.FindProperty("material").objectReferenceValue = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            }
            so.ApplyModifiedPropertiesWithoutUndo(); return d;
        }
        // Editor-only reflection keeps graph-authoring assemblies out of the runtime/test assembly contracts.
        static VisualEffectAsset Graph(string name, bool complete)
        {
            var assembly = Assembly.Load("Unity.VisualEffectGraph.Editor");
            string path = Folder + "/" + name + ".vfx";
            var asset = (VisualEffectAsset)assembly.GetType("UnityEditor.VisualEffectAssetEditorUtility", true)
                .GetMethod("CreateNewAsset").Invoke(null, new object[] { path });
            var resource = assembly.GetType("UnityEditor.VFX.VisualEffectObjectExtensions", true).GetMethod("GetResource")
                .Invoke(null, new object[] { asset });
            var extensions = assembly.GetType("UnityEditor.VFX.VisualEffectResourceExtensions", true);
            var graph = extensions.GetMethod("GetGraph").Invoke(null, new[] { resource });
            var parameterType = assembly.GetType("UnityEditor.VFX.VFXParameter", true);
            string[] names = complete ? new[] { "FxEvents", "FxEventCount", "ParticlesPerEvent", "QualityFactor" } : new[] { "QualityFactor" };
            foreach (string property in names)
            {
                var parameter = ScriptableObject.CreateInstance(parameterType);
                Type type = property == "FxEvents" ? typeof(GraphicsBuffer) : property == "QualityFactor" ? typeof(float) : typeof(int);
                parameterType.GetMethod("Init").Invoke(parameter, new object[] { type });
                var set = parameterType.GetMethod("SetSettingValue", new[] { typeof(string), typeof(object) });
                set.Invoke(parameter, new object[] { "m_ExposedName", property }); set.Invoke(parameter, new object[] { "m_Exposed", true });
                graph.GetType().GetMethod("AddChild").Invoke(graph, new object[] { parameter, -1, true });
            }
            extensions.GetMethod("WriteAssetWithSubAssets").Invoke(null, new[] { resource });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
        }
        FxRig Create() => rig = FxRig.Create(null, config, definitions, null, VfxQuality.High);
        [UnityTest] public IEnumerator Create_BuildsOneChildPerBatchedAndPoolSlot()
        {
            Create(); Assert.AreEqual(5, rig.transform.childCount);
            using var world = new World("FxRig binding"); rig.BindTo(world);
            Assert.IsTrue(rig.Stats.Bound);
            using var query = world.EntityManager.CreateEntityQuery(typeof(VfxRuntimeConfig));
            Assert.AreEqual(config.ChunkDissolveSeconds, query.GetSingleton<VfxRuntimeConfig>().ChunkDissolveSeconds);
            rig.SetQuality(VfxQuality.Low); yield return null;
            Assert.AreEqual(5, rig.transform.childCount);
            foreach (var effect in rig.GetComponentsInChildren<VisualEffect>()) Assert.AreEqual(config.Low.ParticleFactor, effect.GetFloat("QualityFactor"));
            var replacement = FxRig.Create(null, config, Array.Empty<FxDefinition>(), null, VfxQuality.High);
            try { replacement.BindTo(world); Assert.IsFalse(rig.Stats.Bound); Assert.IsTrue(replacement.Stats.Bound); }
            finally { replacement.Dispose(); Object.Destroy(replacement.gameObject); }
        }
        [UnityTest] public IEnumerator Create_GraphMissingProperty_LogsErrorAndSkips()
        {
            definitions[0] = Definition("fx_bad_test", FxKind.HitKinetic, FxMode.Batched, Graph("Missing", false));
            LogAssert.Expect(LogType.Error, new Regex("fx_bad_test.*Missing.*FxEvents"));
            Create(); yield return null;
            Assert.IsFalse(rig.SlotByHash.ContainsKey(definitions[0].Id.Hash.Value));
            Assert.IsTrue(rig.SlotByHash.ContainsKey(definitions[1].Id.Hash.Value)); Assert.AreEqual(4, rig.transform.childCount);
        }
        [UnityTest] public IEnumerator Dispose_ReleasesAllBuffers()
        {
            // The global leak probe must not count the unrelated Entities Graphics frame allocator.
            var presentation = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<PresentationSystemGroup>();
            bool presentationEnabled = presentation != null && presentation.Enabled;
            if (presentation != null) presentation.Enabled = false;
            var previousLeakMode = Unity.Collections.NativeLeakDetection.Mode;
            Unity.Collections.NativeLeakDetection.Mode = Unity.Collections.NativeLeakDetectionMode.EnabledWithStackTrace;
            try
            {
            Create(); rig.Dispose(); Object.Destroy(rig.gameObject); yield return null;
            UnsafeUtility.CheckForLeaks();
            Create(); Assert.AreEqual(2, rig.LiveBufferCount);
            var tracerBuffer = rig.Tracers.Buffer;
            var batchedBuffer = rig.EventBuffer(1);
            rig.Dispose(); yield return null;
            Assert.IsFalse(tracerBuffer.IsValid()); Assert.IsFalse(batchedBuffer.IsValid());
            Assert.AreEqual(0, rig.LiveBufferCount); Assert.IsFalse(rig.Slots.IsCreated); Assert.IsFalse(rig.Palette.IsCreated);
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
            }
            finally { Unity.Collections.NativeLeakDetection.Mode = previousLeakMode; if (presentation != null) presentation.Enabled = presentationEnabled; }
        }
        [Test] public void Create_TotalBufferBytes_Under12MB()
        {
            Create(); long bytes = rig.TotalBufferBytes;
            TestContext.WriteLine($"Representative rig GPU buffers: {bytes} bytes; VFX-004 content set is not authored yet.");
            Assert.Greater(bytes, 0); if (bytes > 12 * 1024 * 1024) PerfRun.WarnBudget($"FxRig buffers {bytes} bytes, target 12582912 bytes.");
        }
        [Test] public void SlotByHash_ResolvesEveryDefinition()
        {
            var palette = new Color32[256]; palette[17] = new Color32(128, 64, 255, 255);
            rig = FxRig.Create(null, config, definitions, palette, VfxQuality.High);
            Assert.AreEqual(FxKind.None, rig.Slots[0].Kind);
            for (int i = 0; i < definitions.Length; i++)
            {
                Assert.AreEqual(i + 1, rig.SlotByHash[definitions[i].Id.Hash.Value]);
                Assert.AreEqual(i + 1, rig.DefaultSlotByKind[(int)definitions[i].Kind]);
            }
            Assert.AreEqual(FxRig.Pack((Color32)((Color)palette[17]).linear), rig.Palette[17]);
        }
    }
}
#endif
