namespace Game.Voxel.Data
{
    /// <summary>Implements SPEC-05 §4.7. Deferred save block: copies a structure at capture (main thread), encodes on the save worker.</summary>
    public sealed class VoxsSnapshotBlock : Game.Core.Save.ISaveDeferredBlock
    {
        public static VoxsSnapshotBlock Capture(in VoxsSource source, VoxsBlockNames names) { throw new System.NotImplementedException("PERS-008"); }   // copies arrays with Allocator.Persistent
        public byte[] Encode() { throw new System.NotImplementedException("PERS-008"); }                                                                // VoxsEncodeOptions.ForSaveChunk
        public void Dispose() { throw new System.NotImplementedException("PERS-008"); }                                                                 // idempotent
    }
}
