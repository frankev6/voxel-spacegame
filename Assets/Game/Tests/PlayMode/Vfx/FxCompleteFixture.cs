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
    public abstract class FxCompleteFixture
    {
        protected FxRig Rig;
        protected EcsTestWorld World;
        protected FxUploadSystem Upload;
        protected FxEventQueue Queue;
        protected NativeArray<FxGpuEvent> Events;
        protected GameObject view;
        [UnitySetUp] public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires real VFX graphics device");
            World = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem),typeof(FxUploadSystem));
            Rig = FxRig.Create(null,FxContentTests.Config(),FxContentTests.Definitions(),null,VfxQuality.High); Rig.BindTo(World.World);
            Upload = World.World.GetExistingSystemManaged<FxUploadSystem>(); Queue = World.GetSingleton<FxEventQueue>();
            Events = new NativeArray<FxGpuEvent>(20000,Allocator.Persistent); Upload.Update();
            view = new GameObject("VFX fixture camera"); var camera = view.AddComponent<Camera>(); camera.transform.position = new Vector3(0,0,-40);
            // Let Unity initialize the real graphs before sending their first event.
            yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Rig != null) { Rig.Dispose(); Object.Destroy(Rig.gameObject); }
            if (view != null) Object.Destroy(view);
            World?.Dispose(); if (Events.IsCreated) Events.Dispose(); yield return null;
        }
        protected VisualEffect Effect(FxKind kind,int index = 0)
        {
            var definition = Rig.Config.Defaults[(int)kind]; string suffix = definition.Mode == FxMode.Pooled ? "_"+index : "";
            return Rig.transform.Find("Fx_" + definition.IdString + suffix).GetComponent<VisualEffect>();
        }
        protected void Submit(FxKind kind,float3 position,int count = 1,float3 velocity = default)
        {
            int slot = Rig.DefaultSlotByKind[(int)kind];
            for (int i = 0; i < count; i++) Events[i] = new FxGpuEvent { position = position, size = .5f, direction = new float3(0,0,1), velocity = velocity,
                color = new float4(1,0,0,1), lifetimeScale = 1, seed = (uint)i + 63001, particleCount = 1, kind = (uint)kind };
            Rig.Submit(slot,Events,0,count);
        }
        protected IEnumerator Alive(FxKind kind)
        {
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (Effect(kind).aliveParticleCount == 0 && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.Greater(Effect(kind).aliveParticleCount,0,"Graph produced actual GPU particles");
        }
    }
}
#endif
