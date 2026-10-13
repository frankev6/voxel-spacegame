#if UNITY_EDITOR
using System;
using System.Collections;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Presentation.Vfx;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;
namespace Game.Tests.PlayMode.Vfx
{
    public sealed class FxOriginShiftTests : FxCompleteFixture
    {
        [UnityTest] public IEnumerator Rebase_ParticlesKeepWorldPositionSameFrame()
        {
            var cameraObject = new GameObject("Rebase camera"); var camera = cameraObject.AddComponent<Camera>();
            var lightObject = new GameObject("Rebase light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            var texture = new RenderTexture(64,64,24); var pixels = new Texture2D(64,64,TextureFormat.RGBA32,false);
            camera.targetTexture = texture; camera.orthographic = true; camera.orthographicSize = 2; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(1990,0,-10);
            Color[] Read()
            { camera.Render(); var previous = RenderTexture.active; RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,64,64),0,0); pixels.Apply(); RenderTexture.active = previous; return pixels.GetPixels(); }
            try
            {
                Submit(FxKind.Debris,new float3(1990,0,0),1000); yield return Alive(FxKind.Debris);
                var effect = Effect(FxKind.Debris); effect.pause = true; int live = effect.aliveParticleCount;
                Read(); var before = Read(); Rig.ShiftAnchors(new float3(-2000,0,0)); camera.transform.position += new Vector3(-2000,0,0);
                var after = Read(); float error = 0, visible = 0;
                for (int i = 0; i < before.Length; i++) { error += Mathf.Abs(before[i].r-after[i].r); visible += before[i].r; }
                Assert.Greater(visible,1,"Actual GPU debris is visible before rebase"); Assert.Less(error/before.Length,.01f,"Rendered cloud stays at the same screen position during rebase");
                var readback = AsyncGPUReadback.Request(Rig.EventBuffer(Rig.DefaultSlotByKind[(int)FxKind.Debris]));
                while (!readback.done) yield return null; Assert.IsFalse(readback.hasError);
                var values = readback.GetData<FxGpuEvent>();
                for (int i = 0; i < 16; i++) Assert.Less(math.distance((float3)values[i].position + Rig.AnchorPositions[Rig.DefaultSlotByKind[(int)FxKind.Debris]],new float3(-10,0,0)),.01f);
                Assert.AreEqual(live,effect.aliveParticleCount,"Rebase preserves live particles without respawn"); effect.pause = false;
            }
            finally { camera.targetTexture = null; texture.Release(); Object.Destroy(texture); Object.Destroy(pixels); Object.Destroy(cameraObject); Object.Destroy(lightObject); }

        }
        [Test] public void HugeDelta_ClearsExceptWarp()
        {
            World.AddSingleton(new OriginShiftRecord { Sequence = 1 }); Upload.Update(); Submit(FxKind.Warp,new float3(4,0,0)); Submit(FxKind.Debris,float3.zero);
            using var q = World.EntityManager.CreateEntityQuery(typeof(OriginShiftRecord)); q.SetSingleton(new OriginShiftRecord { Sequence = 2, Delta = new float3(30000,0,0) });
            uint revision = Rig.ClearRevision; Upload.Update(); Assert.Greater(Rig.ClearRevision,revision); Assert.AreEqual(4,Effect(FxKind.Warp).transform.position.x); Assert.AreEqual(0,Rig.Stats.DebrisLiveEstimate);
        }
        [Test] public void StarSystemEntered_ClearsExceptWarp()
        { Submit(FxKind.Warp,new float3(4,0,0)); uint revision = Rig.ClearRevision; EventBus<StarSystemEntered>.Publish(new StarSystemEntered(1,StarSystemEntryReason.Jump,1)); EventBus<StarSystemEntered>.Drain(); Assert.Greater(Rig.ClearRevision,revision); Assert.AreEqual(4,Effect(FxKind.Warp).transform.position.x); }
        [Test] public void SessionReset_ClearsAll()
        { Submit(FxKind.Warp,new float3(4,0,0)); EventBus<SessionReset>.Publish(default); EventBus<SessionReset>.Drain(); Assert.AreEqual(Vector3.zero,Effect(FxKind.Warp).transform.position); }
        [Test] public void IdleAnchor_ResetsToOrigin()
        { Rig.Now = 0; Submit(FxKind.Debris,float3.zero); Rig.ShiftAnchors(new float3(-2000,0,0)); Rig.ResetIdleAnchors(7); Assert.AreEqual(float3.zero,Rig.AnchorPositions[Rig.DefaultSlotByKind[(int)FxKind.Debris]]); Assert.AreEqual(Vector3.zero,Effect(FxKind.Debris).transform.position); }
    }
}
#endif
