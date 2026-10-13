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
    public sealed class FxRegressionTests : FxCompleteFixture
    {
        void Frame() { for (int i = 0; i < 10000; i++) Queue.Queue.Enqueue(new FxEvent { Kind = FxKind.HitKinetic, Scale = 1 }); Upload.Update(); }
        [UnityTest] public IEnumerator Hits10k_NoNewGameObjects()
        {
            for (int i = 0; i < 10; i++) { Frame(); yield return null; }
            int count = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Length;
            for (int i = 0; i < 100; i++) { Frame(); yield return null; }
            Assert.AreEqual(count,Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Length);
        }
        [UnityTest] public IEnumerator Hits10k_NoGcAlloc()
        {
            for (int i = 0; i < 10; i++) { Frame(); yield return null; }
            long total = 0;
            for (int i = 0; i < 100; i++) { long before = GC.GetAllocatedBytesForCurrentThread(); Frame(); total += GC.GetAllocatedBytesForCurrentThread()-before; yield return null; }
            Assert.AreEqual(0,total,"Warmed real queue, upload, GraphicsBuffer and event submission path");
        }
    }
}
#endif
