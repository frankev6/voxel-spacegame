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
    public sealed class FxQualityTests : FxCompleteFixture
    {
        [Test] public void EachLevel_EnforcesBudgetRow()
        {
            int[] caps = {10000,25000,50000}, spawns = {4000,10000,20000}; float[] factors = {.25f,.5f,1};
            for (int i = 0; i < 3; i++)
            {
                Rig.SetQuality((VfxQuality)i); Assert.AreEqual(caps[i],Rig.Budget.DebrisLiveCap); Assert.AreEqual(spawns[i],Rig.Budget.DebrisSpawnPerFrame);
                Assert.AreEqual(4096 << i,Rig.Budget.MaxTracers); Assert.AreEqual(64 << i,Rig.Budget.MaxBeams); Assert.AreEqual(256 << i,Rig.Budget.MaxPlumes); Assert.AreEqual(32 << i,Rig.Budget.MaxShields);
                foreach (var e in Rig.GetComponentsInChildren<VisualEffect>()) Assert.AreEqual(factors[i],e.GetFloat("QualityFactor"));
                for (int n = 0; n < 20000; n++) Queue.Queue.Enqueue(new FxEvent { Kind = FxKind.Debris, Scale = 1 }); Upload.Update();
                Assert.LessOrEqual(Rig.Stats.DebrisSpawned,spawns[i]); Assert.LessOrEqual(Rig.Stats.DebrisLiveEstimate,caps[i]);
            }
        }
        [Test] public void Switch_NoReallocation()
        {
            var buffer = Rig.Tracers.Buffer; long bytes = Rig.TotalBufferBytes; int count = Rig.LiveBufferCount;
            Rig.SetQuality(VfxQuality.Low); Rig.SetQuality(VfxQuality.High);
            Assert.AreSame(buffer,Rig.Tracers.Buffer); Assert.AreEqual(bytes,Rig.TotalBufferBytes); Assert.AreEqual(count,Rig.LiveBufferCount);
        }
    }
}
#endif
