using Game.Data.Streams;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-012: GuardedHandles orders a writer and two readers in different systems with no safety errors (SPEC-02 §4.12, §5.13).</summary>
    public sealed partial class GuardedHandlesTests
    {
        public struct Shared : IComponentData { public GuardedHandles Access; public NativeArray<int> Values; }

        [BurstCompile] struct WriteJob : IJob
        {
            public NativeArray<int> Values; public int Tick;
            public void Execute() { for (int i = 0; i < Values.Length; i++) Values[i] = Tick * 1000 + i; }
        }

        [BurstCompile] struct ReadJob : IJob
        {
            [ReadOnly] public NativeArray<int> Values; public NativeReference<long> Sum;
            public void Execute() { long s = 0; for (int i = 0; i < Values.Length; i++) s += Values[i]; Sum.Value = s; }
        }

        [DisableAutoCreation]
        public partial class Writer : SystemBase
        {
            public int Tick; EntityQuery q;
            protected override void OnCreate() { q = GetEntityQuery(ComponentType.ReadOnly<Shared>()); }
            protected override void OnUpdate()
            {
                var s = q.GetSingleton<Shared>();
                Dependency = new WriteJob { Values = s.Values, Tick = ++Tick }.Schedule(s.Access.Acquire(Dependency));
                s.Access.Release(Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class Reader : SystemBase
        {
            public NativeReference<long> Sum; EntityQuery q;
            protected override void OnCreate() { q = GetEntityQuery(ComponentType.ReadOnly<Shared>()); }
            protected override void OnUpdate()
            {
                var s = q.GetSingleton<Shared>();
                Dependency = new ReadJob { Values = s.Values, Sum = Sum }.Schedule(s.Access.BeginRead(Dependency));
                s.Access.EndRead(Dependency);
            }
        }

        [Test]
        public void WriterAndTwoReaders_NoSafetyErrors_CompleteAccessFinishesEverything()
        {
            const int N = 100_000;
            using var world = new World("guarded-handles");
            var em = world.EntityManager;
            var shared = new Shared { Access = GuardedHandles.Create(), Values = new NativeArray<int>(N, Allocator.Persistent) };
            em.AddComponentData(em.CreateEntity(), shared);
            var writer = world.CreateSystemManaged<Writer>();
            var r1 = world.CreateSystemManaged<Reader>(); r1.Sum = new NativeReference<long>(Allocator.Persistent);
            var r2 = world.CreateSystemManaged<Reader>(); r2.Sum = new NativeReference<long>(Allocator.Persistent);
            try
            {
                for (int tick = 0; tick < 10; tick++) { writer.Update(); r1.Update(); r2.Update(); }
                shared.Access.CompleteAccess();
                Assert.IsTrue(shared.Access.Exclusive.Value.IsCompleted && shared.Access.Readers.Value.IsCompleted, "no running job after CompleteAccess");
                long expected = 0; for (int i = 0; i < N; i++) expected += 10 * 1000 + i;
                Assert.AreEqual(expected, r1.Sum.Value); Assert.AreEqual(expected, r2.Sum.Value);
                shared.Values[0] = 0;                                   // main-thread write: throws if any job still uses the array
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                shared.Access.Dispose();
                shared.Values.Dispose(); r1.Sum.Dispose(); r2.Sum.Dispose();
            }
        }
    }
}
