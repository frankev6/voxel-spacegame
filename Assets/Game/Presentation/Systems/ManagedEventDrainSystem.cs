using System.Collections.Generic;
using System.Reflection;
using Game.Core.Events;
using Game.Data.Components;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Presentation.Systems
{
    /// <summary>Moves ECS-produced events into EventBus&lt;T&gt; and drains EventHub once per frame (architecture §5, SPEC-02 §4.6, §5.6).</summary>
    [UpdateInGroup(typeof(Game.Core.Systems.PresentationBridgeGroup), OrderLast = true)]
    public partial class ManagedEventDrainSystem : SystemBase
    {
        List<IManagedEventChannel> channels;
        HashSet<System.Type> registered;

        /// <summary>Idempotent; main thread, outside a fixed-step update (structural change). Creates the singleton entity.</summary>
        public void RegisterChannel<T>() where T : unmanaged
        {
            if (!registered.Add(typeof(T))) return;
            var em = EntityManager;
            var entity = em.CreateEntity(ComponentType.ReadWrite<ManagedEventQueue<T>>());
            em.SetName(entity, "ManagedEventQueue<" + typeof(T).Name + ">");
            em.SetComponentData(entity, new ManagedEventQueue<T> { Queue = new NativeQueue<ManagedEventEnvelope<T>>(Allocator.Persistent) });
            channels.Add(new ManagedEventChannel<T>(GetEntityQuery(ComponentType.ReadWrite<ManagedEventQueue<T>>())));
        }

        public int ChannelCount => channels.Count;

        protected override void OnCreate()
        {
            channels = new List<IManagedEventChannel>();
            registered = new HashSet<System.Type>();
            ManagedEventChannels.RegisterAll(this);
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            for (int i = 0; i < channels.Count; i++) channels[i].Transfer(em);
            EventHub.DrainAll();
        }

        protected override void OnDestroy()
        {
            var em = EntityManager;
            for (int i = 0; i < channels.Count; i++) channels[i].Dispose(em);
            channels.Clear();
            registered.Clear();
        }
    }

    /// <summary>Transfers and disposes one channel's queue (internal, SPEC-02 §4.6, §5.6).</summary>
    interface IManagedEventChannel
    {
        void Transfer(EntityManager em);
        void Dispose(EntityManager em);
    }

    /// <summary>Channel of one payload type, holding the query of its singleton (internal, SPEC-02 §4.6, §5.6).</summary>
    sealed class ManagedEventChannel<T> : IManagedEventChannel where T : unmanaged
    {
        readonly EntityQuery query;
        NativeArray<int2> spans;                               // the payload's field bytes (offset, length), for the tie-break hash

        public ManagedEventChannel(EntityQuery query)
        {
            this.query = query;
            spans = PayloadLayout.Spans(typeof(T), Allocator.Persistent);
        }

        /// <summary>SPEC-02 §5.6 step 4: complete every producer, take the frame's batch, sort it, publish in order.</summary>
        public void Transfer(EntityManager em)
        {
            em.CompleteDependencyBeforeRW<ManagedEventQueue<T>>();
            if (!query.TryGetSingleton(out ManagedEventQueue<T> singleton) || !singleton.Queue.IsCreated) return;
            var queue = singleton.Queue;
            if (queue.IsEmpty()) return;
            var batch = queue.ToArray(Allocator.Temp);
            queue.Clear();
            batch.Sort(new EnvelopeComparer<T> { Spans = spans });
            for (int i = 0; i < batch.Length; i++) EventBus<T>.Publish(batch[i].Payload);
            batch.Dispose();
        }

        public void Dispose(EntityManager em)
        {
            em.CompleteDependencyBeforeRW<ManagedEventQueue<T>>();
            if (query.TryGetSingleton(out ManagedEventQueue<T> singleton) && singleton.Queue.IsCreated) singleton.Queue.Dispose();
            if (spans.IsCreated) spans.Dispose();
        }
    }

    /// <summary>Orders a frame's batch by (Tick, SortKey, payload hash) (internal, SPEC-02 §5.6 step 4). The payload hash covers field bytes
    /// only, never padding, whose contents are undefined (SPEC-02 open question 13); it is computed only when Tick and SortKey tie.</summary>
    struct EnvelopeComparer<T> : IComparer<ManagedEventEnvelope<T>> where T : unmanaged
    {
        public NativeArray<int2> Spans;

        public int Compare(ManagedEventEnvelope<T> x, ManagedEventEnvelope<T> y)
        {
            int c = x.Tick.CompareTo(y.Tick);
            if (c != 0) return c;
            c = x.SortKey.CompareTo(y.SortKey);
            if (c != 0) return c;
            return PayloadLayout.Hash(x.Payload, Spans).CompareTo(PayloadLayout.Hash(y.Payload, Spans));
        }
    }

    /// <summary>Byte ranges of a payload's fields, padding excluded, and the xxHash3 over them (internal, SPEC-02 §5.6 step 4).</summary>
    static class PayloadLayout
    {
        /// <summary>Main thread, once per channel: (offset, length) of every field, nested structs flattened, adjacent ranges merged.</summary>
        public static NativeArray<int2> Spans(System.Type type, Allocator allocator)
        {
            var list = new List<int2>();
            Collect(type, 0, list);
            list.Sort((a, b) => a.x.CompareTo(b.x));
            var merged = new List<int2>();
            foreach (var s in list)
            {
                if (merged.Count > 0 && merged[merged.Count - 1].x + merged[merged.Count - 1].y == s.x)
                    merged[merged.Count - 1] = new int2(merged[merged.Count - 1].x, merged[merged.Count - 1].y + s.y);
                else merged.Add(s);
            }
            var spans = new NativeArray<int2>(merged.Count, allocator);
            for (int i = 0; i < merged.Count; i++) spans[i] = merged[i];
            return spans;
        }

        static void Collect(System.Type type, int baseOffset, List<int2> spans)
        {
            if (type.IsPrimitive || type.IsEnum || type.IsPointer) { spans.Add(new int2(baseOffset, UnsafeUtility.SizeOf(type))); return; }
            foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                int offset = baseOffset + UnsafeUtility.GetFieldOffset(f);
                if (f.GetCustomAttribute<System.Runtime.CompilerServices.FixedBufferAttribute>() != null)
                    spans.Add(new int2(offset, UnsafeUtility.SizeOf(f.FieldType)));
                else Collect(f.FieldType, offset, spans);
            }
        }

        public static unsafe ulong Hash<T>(in T payload, NativeArray<int2> spans) where T : unmanaged
        {
            ulong h = 0;
            fixed (T* p = &payload)
            {
                for (int i = 0; i < spans.Length; i++)
                {
                    var r = xxHash3.Hash64((byte*)p + spans[i].x, spans[i].y, h);
                    h = ((ulong)r.y << 32) | r.x;
                }
            }
            return h;
        }
    }
}
