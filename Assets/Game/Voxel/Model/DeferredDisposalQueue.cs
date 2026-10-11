using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. Singleton; appended on the main thread only.</summary>
    public struct DeferredDisposalQueue : IComponentData
    {
        public NativeList<RetiredBlob> Blobs; public NativeList<RetiredMesh> Meshes; public uint Frame;
        public uint Tick;          // SimClock.Tick mirrored by DeferredDisposalSystem each frame (0 when no SimClock)
        public byte HasClock;      // 1 while a SimClock singleton exists
    }
}
