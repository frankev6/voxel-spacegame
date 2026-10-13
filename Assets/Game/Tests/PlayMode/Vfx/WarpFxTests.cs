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
    public sealed class WarpFxTests : FxCompleteFixture
    {
        [UnityTest] public IEnumerator WarpEvent_PlaysPooledInstance()
        {
            Queue.Queue.Enqueue(new FxEvent { Kind = FxKind.Warp, Pos = new float3(2,3,4), Dir = new float3(0,0,1), Scale = 1 }); Upload.Update();
            Assert.AreEqual(1,Rig.Stats.PooledPlayed); Assert.AreEqual(new Vector3(2,3,4),Effect(FxKind.Warp).transform.position); yield return Alive(FxKind.Warp);
        }
        [Test] public void ThirdWarp_StealsOldest()
        { Submit(FxKind.Warp,new float3(1,0,0)); Submit(FxKind.Warp,new float3(2,0,0)); Submit(FxKind.Warp,new float3(3,0,0)); Assert.AreEqual(3,Effect(FxKind.Warp,0).transform.position.x); Assert.AreEqual(2,Effect(FxKind.Warp,1).transform.position.x); }
    }
}
#endif
