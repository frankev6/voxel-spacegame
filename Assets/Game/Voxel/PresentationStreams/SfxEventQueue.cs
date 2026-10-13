using Game.Data.Streams;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Singleton. Owned by SfxEventQueueCreateSystem. Implements SPEC-64 §4.1.</summary>
    public struct SfxEventQueue : IComponentData { public NativeQueue<SfxEvent> Queue; public GuardedHandles Access; }
}
