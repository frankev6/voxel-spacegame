namespace Game.Core.Save
{
    /// <summary>A payload part encoded later on the save worker (e.g. SPEC-05 VoxsSnapshotBlock). Implements SPEC-02 §4.7.</summary>
    public interface ISaveDeferredBlock : System.IDisposable
    {
        byte[] Encode();                                     // called once, on the save worker; then Dispose() on the worker
    }
}
