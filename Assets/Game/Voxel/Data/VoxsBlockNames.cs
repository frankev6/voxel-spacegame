namespace Game.Voxel.Data
{
    /// <summary>Implements SPEC-05 §4.7. Immutable BlockIndex → ContentId string table, safe to read from the save worker thread.</summary>
    public sealed class VoxsBlockNames
    {
        public static VoxsBlockNames FromCatalog(Game.Data.Services.ContentCatalog catalog) { throw new System.NotImplementedException("PERS-007"); }   // main thread
        public string Get(ushort blockIndex) { throw new System.NotImplementedException("PERS-007"); }                                                  // null for Air
    }
}
