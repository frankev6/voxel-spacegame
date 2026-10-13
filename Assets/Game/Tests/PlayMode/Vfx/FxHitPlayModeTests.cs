#if UNITY_EDITOR
using System.Collections;
using Game.Data.Definitions;
using Game.Presentation.Vfx;
using Game.Tests.Common;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.VFX;
namespace Game.Tests.PlayMode.Vfx
{
    public sealed class FxHitPlayModeTests
    {
        [UnityTest] public IEnumerator HundredHits_OneBatch_ParticlesAlive()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires VFX graphics device.");
            using var world = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem), typeof(FxUploadSystem));
            var rig = FxRig.Create(null, FxContentTests.Config(), FxContentTests.Definitions(), null, VfxQuality.High);
            var cameraObject = new GameObject("FX verification camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -12); camera.backgroundColor = Color.black; camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(512, 512, 24); target.Create(); camera.targetTexture = target;
            float previousCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            try
            {
                rig.BindTo(world.World);
                for (int i = 0; i < 8; i++) yield return null; // shader/resource warm-up before the measured five-frame window
                using var query = world.EntityManager.CreateEntityQuery(typeof(FxEventQueue)); var queue = query.GetSingleton<FxEventQueue>().Queue;
                for (int i = 0; i < 100; i++) queue.Enqueue(FxEvents.Hit(FxKind.HitKinetic, new float3((i % 10 - 4.5f) * .6f, (i / 10 - 4.5f) * .6f, 0), new float3(0, 0, -1), float3.zero, 50));
                world.World.GetExistingSystemManaged<FxUploadSystem>().Update();
                Assert.AreEqual(1, rig.Stats.BatchesSubmitted); Assert.AreEqual(100, rig.Stats.EventsDrained);
                var effect = rig.transform.Find("Fx_fx_hit_kinetic_default").GetComponent<VisualEffect>();
                Assert.AreEqual(800, effect.GetInt("FxEventCount") * effect.GetInt("ParticlesPerEvent"));
                int alive = 0;
                for (int frame = 0; frame < 5 && alive == 0; frame++) { yield return null; alive = effect.aliveParticleCount; }
                Assert.Greater(alive, 0, "Actual GPU particles within five frames");
                TestContext.WriteLine($"100 hits: one batch, 800 spawn requests, {alive} particles alive.");
            }
            finally
            {
                Time.captureDeltaTime = previousCapture; rig.Dispose(); Object.Destroy(rig.gameObject);
                Object.Destroy(cameraObject); target.Release(); Object.Destroy(target);
            }
        }
    }
}
#endif
