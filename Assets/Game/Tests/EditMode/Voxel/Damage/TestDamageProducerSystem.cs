using Game.Data.Streams;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Tests.EditMode.Voxel.Damage
{
    internal struct TestDamageIndex : IComponentData { public int Value; }
    internal struct TestDamageTagA : IComponentData { }
    internal struct TestDamageTagB : IComponentData { }

    /// <summary>DMG-002 producer fixture: the supplied event array is indexed by queried entities and written per chunk.</summary>
    [DisableAutoCreation]
    internal partial class TestDamageProducerSystem : SystemBase
    {
        internal ComponentType Tag;
        internal NativeArray<DamageEvent> Events;
        internal bool Explosions;
        EntityQuery query, streams;
        protected override void OnCreate() => streams = GetEntityQuery(ComponentType.ReadOnly<DamageStreams>());
        protected override void OnUpdate()
        {
            if (query == default) query = GetEntityQuery(Tag, ComponentType.ReadOnly<TestDamageIndex>());
            int count = query.CalculateChunkCountWithoutFiltering();
            var data = streams.GetSingleton<DamageStreams>();
            var writer = Explosions ? data.Explosions.BeginWrite(ref CheckedStateRef, Game.Data.Combat.ProducerKind.Test, count)
                : data.Damage.BeginWrite(ref CheckedStateRef, Game.Data.Combat.ProducerKind.Test, count);
            Dependency = new WriteJob { Writer = writer, Events = Events, Indices = GetComponentTypeHandle<TestDamageIndex>(true), Explosions = Explosions }.ScheduleParallel(query, Dependency);
            if (Explosions) data.Explosions.EndWrite(Dependency); else data.Damage.EndWrite(Dependency);
        }
        [BurstCompile] struct WriteJob : IJobChunk
        {
            public NativeStream.Writer Writer;
            [ReadOnly] public NativeArray<DamageEvent> Events;
            [ReadOnly] public ComponentTypeHandle<TestDamageIndex> Indices;
            public bool Explosions;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var indices = chunk.GetNativeArray(ref Indices);
                Writer.BeginForEachIndex(unfilteredChunkIndex);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var e = Events[indices[i].Value];
                    if (Explosions) Writer.Write(new ExplosionRequest { Source = e.Source, Producer = e.Producer, Sequence = e.Sequence, Amount = e.Amount });
                    else Writer.Write(e);
                }
                Writer.EndForEachIndex();
            }
        }
    }
    [DisableAutoCreation]
    internal partial class TestDamageConsumerSystem : SystemBase
    {
        internal NativeList<DamageEvent> Damage;
        internal NativeList<ExplosionRequest> Explosions;
        internal bool DrainExplosions;
        internal JobHandle LastJob;
        EntityQuery streams;
        protected override void OnCreate() => streams = GetEntityQuery(ComponentType.ReadOnly<DamageStreams>());
        protected override void OnUpdate()
        {
            var data = streams.GetSingleton<DamageStreams>();
            if (DrainExplosions) Dependency = data.ScheduleDrainExplosions(ref CheckedStateRef, Explosions);
            else
            {
                var set = data.Damage.BeginRead(ref CheckedStateRef);
                Dependency = SimEventGather.Schedule(set, Damage, Dependency);
                data.Damage.EndRead(Dependency);
            }
            LastJob = Dependency;
        }
    }
}
