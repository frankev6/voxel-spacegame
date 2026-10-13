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
    [Category("Performance")] public sealed class FxUploadPerfTests : FxCompleteFixture
    {
        IEnumerator Measure(FxKind kind,int count,double target)
        {
            var samples = new double[300]; var clock = new System.Diagnostics.Stopwatch();
            for (int frame = 0; frame < 360; frame++)
            {
                Rig.ClearAll(false); for (int i = 0; i < count; i++) Queue.Queue.Enqueue(new FxEvent { Kind = kind == FxKind.Debris ? kind : i % 3 == 0 ? FxKind.HitKinetic : i % 3 == 1 ? FxKind.HitEnergy : FxKind.Muzzle, Scale = .5f });
                clock.Restart(); Upload.Update(); clock.Stop(); if (frame >= 60) samples[frame-60] = clock.Elapsed.TotalMilliseconds; yield return null;
            }
            PerfRun.Gate("FxUploadSystem",samples,target);
        }
        [UnityTest] public IEnumerator Upload5k_MainThread_Smoke() => Measure(FxKind.HitKinetic,5000,.25);
        [UnityTest] public IEnumerator Upload20kDebris_MainThread() => Measure(FxKind.Debris,20000,.6);
    }
}
#endif
