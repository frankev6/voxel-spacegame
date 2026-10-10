namespace Game.Data.Blocks
{
    /// <summary>Builds BlockDefsBlob and BlockModulesBlob from the catalog (SPEC-04 §4.7, §5.5).</summary>
    public static class BlockDefsBlobBuilder
    {
        /// <summary>§5.5. propMeshes receives the prop mesh table (index = BlockDef.PropMeshIndex); modules receives the module rows.</summary>
        public static Unity.Entities.BlobAssetReference<BlockDefsBlob> Build(
            Game.Data.Services.ContentCatalog catalog,
            Game.Data.Validation.ValidationReport report,
            System.Collections.Generic.List<UnityEngine.Mesh> propMeshes,
            Unity.Collections.Allocator allocator,
            out Unity.Entities.BlobAssetReference<BlockModulesBlob> modules) { throw new System.NotImplementedException("DATA-006"); }
    }
}
