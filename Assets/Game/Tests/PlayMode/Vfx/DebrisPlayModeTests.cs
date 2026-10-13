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
    public sealed class DebrisPlayModeTests : FxCompleteFixture
    {
        [UnityTest] public IEnumerator Stress_LiveEstimateRespectsCapPerQuality()
        {
            foreach (VfxQuality q in new[] { VfxQuality.High,VfxQuality.Medium,VfxQuality.Low })
            {
                Rig.ClearAll(false); Rig.SetQuality(q);
                for (int frame = 0; frame < 600; frame++)
                {
                    for (int i = 0; i < 20000; i++) Queue.Queue.Enqueue(new FxEvent { Kind = FxKind.Debris, Scale = .5f });
                    World.World.SetTime(new Unity.Core.TimeData(frame / 60.0,1f/60)); Upload.Update();
                    Assert.LessOrEqual(Rig.Stats.DebrisLiveEstimate,Rig.Budget.DebrisLiveCap); Assert.LessOrEqual(Rig.Stats.DebrisSpawned,Rig.Budget.DebrisSpawnPerFrame);
                    yield return null;
                }
            }
        }
        [UnityTest] public IEnumerator Debris_ColourAndVelocityFromEvent()
        {
            var cameraObject = new GameObject("Debris test camera"); var camera = cameraObject.AddComponent<Camera>();
            var lightObject = new GameObject("Debris test light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            var texture = new RenderTexture(64,64,24); var pixels = new Texture2D(64,64,TextureFormat.RGBA32,false);
            camera.targetTexture = texture; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.orthographicSize = 4; camera.transform.position = new Vector3(0,0,-10);
            Vector2 Read()
            {
                var previous = RenderTexture.active; RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,64,64),0,0); pixels.Apply(); RenderTexture.active = previous;
                float x = 0, weight = 0, green = 0;
                foreach (var c in pixels.GetPixels()) { weight += c.r; green += c.g; }
                var colors = pixels.GetPixels(); for (int n = 0; n < colors.Length; n++) x += (n%64)*colors[n].r;
                Assert.Greater(weight,1,"Visible red debris in the 64x64 render"); Assert.Greater(weight,green*2); return new Vector2(x/weight,weight);
            }
            try
            {
                Submit(FxKind.Debris,float3.zero,1,new float3(10,0,0)); yield return Alive(FxKind.Debris); yield return null; camera.Render();
                float start = Read().x; yield return new WaitForSecondsRealtime(.15f); yield return null; camera.Render(); Assert.Greater(Read().x,start+3,"Red particle moves along the supplied +X velocity");
            }
            finally { camera.targetTexture = null; texture.Release(); Object.Destroy(texture); Object.Destroy(pixels); Object.Destroy(cameraObject); Object.Destroy(lightObject); }
        }
    }
}
#endif
