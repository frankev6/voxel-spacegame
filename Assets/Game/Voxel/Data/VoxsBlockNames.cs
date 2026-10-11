using System;
using Game.Data.Services;

namespace Game.Voxel.Data
{
    /// <summary>SPEC-05 §4.7: immutable names copied on the main thread for worker encoding.</summary>
    public sealed class VoxsBlockNames
    {
        readonly string[] names;
        VoxsBlockNames(string[] names) { this.names = names; }
        public static VoxsBlockNames FromCatalog(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var names = new string[catalog.BlockCount + 1];
            for (int i = 1; i < names.Length; i++) names[i] = catalog.GetBlock((ushort)i).Id.Value;
            return new VoxsBlockNames(names);
        }
        public string Get(ushort blockIndex) => blockIndex < names.Length ? names[blockIndex] : null;
    }
}
