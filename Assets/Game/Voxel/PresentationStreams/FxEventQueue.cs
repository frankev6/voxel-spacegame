using Game.Data.Streams;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-63 §4.1; queue ownership follows ADR-0050.</summary>
    public struct FxEventQueue : IComponentData
    {
        public NativeQueue<FxEvent> Queue;
        // Main-thread scheduling only. Atomic enqueues use BeginRead/EndRead; drain/clear is exclusive.
        public GuardedHandles Access;
    }
}
