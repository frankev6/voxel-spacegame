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
    [Category("Performance")] public sealed class DebrisGpuBenchmarkTests : FxCompleteFixture
    {
        [UnityTest] public IEnumerator Debris50k_GpuDelta_UnderBudget()
        {
            view.SetActive(false);
            var cameraObject = new GameObject("Debris benchmark camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0,60,-120); camera.transform.LookAt(Vector3.zero);
            var target = new RenderTexture(1920,1080,24); camera.targetTexture = target;
            var timings = new FrameTiming[1]; var with = new System.Collections.Generic.List<double>(); var without = new System.Collections.Generic.List<double>();
            var effect = Effect(FxKind.Debris); effect.SetFloat("DebrisLifetimeMax",20);
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    Rig.ClearAll(false);
                    if (pass == 0)
                    {
                        var random = new Unity.Mathematics.Random(63001);
                        for (int batch = 0; batch < 3; batch++)
                        {
                            int count = batch == 2 ? 10000 : 20000;
                            for (int i = 0; i < count; i++) Events[i] = new FxGpuEvent { position = random.NextFloat3Direction()*random.NextFloat(0,50), velocity = random.NextFloat3Direction()*random.NextFloat(0,2), size = .5f, lifetimeScale = 1, color = new float4(1), particleCount = 1, seed = random.NextUInt() };
                            Rig.Submit(Rig.DefaultSlotByKind[(int)FxKind.Debris],Events,0,count); yield return null;
                        }
                        yield return Alive(FxKind.Debris);
                        double until = Time.realtimeSinceStartupAsDouble + 5;
                        while (effect.aliveParticleCount < 50000 && Time.realtimeSinceStartupAsDouble < until) yield return null;
                        Assert.AreEqual(50000,effect.aliveParticleCount,"Benchmark starts with the actual 50k GPU population");
                    }
                    if (pass == 1) Rig.Unbind(World.World);
                    ulong previous = 0;
                    for (int frame = 0; frame < 360; frame++)
                    {
                        FrameTimingManager.CaptureFrameTimings(); yield return null;
                        if (frame >= 60 && FrameTimingManager.GetLatestTimings(1,timings)>0 && timings[0].gpuFrameTime>0 && timings[0].frameStartTimestamp != previous)
                        { previous = timings[0].frameStartTimestamp; (pass == 0 ? with : without).Add(timings[0].gpuFrameTime); }
                    }
                }
                if (with.Count < 300 || without.Count < 300) { new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("G-CMB-DEBRIS-50K",Array.Empty<double>()).Write("G-CMB-DEBRIS-50K",1); PerfRun.WarnBudget("GPU debris timing unavailable: platform did not return enough unique positive GPU frame samples; no timing claim."); yield break; }
                with.Sort(); without.Sort(); double baseline = without[without.Count/2]; var delta = with.ConvertAll(v => Math.Max(0,v-baseline)).ToArray(); PerfRun.Gate("G-CMB-DEBRIS-50K",delta,1);
            }
            finally { effect.SetFloat("DebrisLifetimeMax",Rig.Config.DebrisLifetimeMax); camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(cameraObject); }
        }
    }
}
#endif
