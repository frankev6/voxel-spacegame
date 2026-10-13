using System.Diagnostics;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Components;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Systems;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.PlayMode.Galaxy
{
    [Category("Performance")]
    public sealed class OriginRebasePerfTests
    {
        [Test] public void Rebase_5000Roots()
        {
            using var world = EcsTestWorld.Create(typeof(OriginRebaseSystem));
            var em = world.EntityManager; world.AddGameplayActive(); world.AddWorldSceneActive();
            using var events = new NativeQueue<ManagedEventEnvelope<OriginShifted>>(Allocator.Persistent);
            world.AddSingleton(new ManagedEventQueue<OriginShifted> { Queue = events });
            using var query = em.CreateEntityQuery(typeof(FrameOrigin)); var origin = query.GetSingletonEntity();
            using var roots = new NativeArray<Entity>(5000, Allocator.Temp);
            em.CreateEntity(em.CreateArchetype(typeof(LocalTransform), typeof(UniversePosition)), roots);
            for (int i = 0; i < roots.Length; i++)
            {
                var position = new float3(i, 0, i % 37);
                em.SetComponentData(roots[i], LocalTransform.FromPosition(position));
                em.SetComponentData(roots[i], new UniversePosition { Value = position });
            }
            var samples = new double[300]; var watch = new Stopwatch();
            for (int i = 0; i < 360; i++)
            {
                em.SetComponentData(origin, new OriginRebaseRequest { StarSystem = StarSystemId.None, NewOrigin = new double3(i * 1500, 0, 0), Serial = (uint)i + 1 });
                em.SetComponentEnabled<OriginRebaseRequest>(origin, true);
                watch.Restart(); world.UpdateSystem<OriginRebaseSystem>(); em.CompleteAllTrackedJobs(); watch.Stop();
                if (i >= 60) samples[i - 60] = watch.Elapsed.TotalMilliseconds;
                events.Clear();
            }
            Assert.AreEqual(360u, em.GetComponentData<FrameOrigin>(origin).ShiftSequence);
            Assert.AreEqual(new double3(4999, 0, 4999 % 37), em.GetComponentData<UniversePosition>(roots[4999]).Value);
            // End-to-end scheduling + completion is a conservative bound on total job time, not a pure worker-time measurement.
            PerfRun.Gate("OriginRebase5000Roots", samples, .5);
        }
    }
}
