namespace Game.Core.Save
{
    /// <summary>JSON for non-voxel payloads (implemented by SPEC-05 NewtonsoftSaveJsonCodec; Core has no Newtonsoft). Implements SPEC-02 §4.7.</summary>
    public interface ISaveJsonCodec
    {
        byte[] Serialize<T>(T value);
        T Deserialize<T>(System.ReadOnlySpan<byte> utf8);
    }
}
