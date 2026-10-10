namespace Game.Data.Blocks
{
    /// <summary>Managed context for baking module rows (boot and editor). Implements SPEC-04 §4.6.</summary>
    public readonly struct BlockModuleBakeContext
    {
        public readonly Game.Data.Services.ContentCatalog Catalog;
        public readonly Game.Data.Definitions.BlockDefinition Block;
        public BlockModuleBakeContext(Game.Data.Services.ContentCatalog catalog, Game.Data.Definitions.BlockDefinition block) { Catalog = catalog; Block = block; }
    }
}
