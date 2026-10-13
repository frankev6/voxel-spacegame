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
    [Category("Performance")] public sealed class TracerPerfTests : FxCompleteFixture
    {
        [UnityTest] public IEnumerator Tracers5k_Smoke()
        {
            using var rowsOwner = new NativeArray<Game.Simulation.Weapons.ProjectileRow>(5000,Allocator.Persistent); var rows = rowsOwner;
            for (int i = 0; i < rows.Length; i++) rows[i] = new Game.Simulation.Weapons.ProjectileRow { Position = new float3(i%100,0,i/100), Velocity = new float3(0,0,100) };
            var samples = new double[300]; var clock = new System.Diagnostics.Stopwatch();
            for (int frame = 0; frame < 360; frame++)
            {
                clock.Restart(); var job = new TracerGatherJob { Rows = rows, Output = Rig.Tracers.Data, Width = .15f, LengthSeconds = .02f, MinLength = .5f, MaxLength = 12, Color0 = new float4(1) };
                Unity.Jobs.IJobParallelForExtensions.Schedule(job,5000,256).Complete(); Rig.Tracers.Count = 5000; Rig.Tracers.Draw(); clock.Stop();
                if (frame >= 60) samples[frame-60] = clock.Elapsed.TotalMilliseconds; yield return null;
            }
            PerfRun.Gate("TracerGatherAndDraw",samples,.3);
        }
    }
}
#endif
