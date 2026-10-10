namespace Game.Data.Components
{
    /// <summary>ECS singleton holding the module rows (§4.6). Created by ContentService.CreateSingletons with BlockDefsRef. Implements SPEC-04 §4.7.</summary>
    public struct BlockModulesRef : Unity.Entities.IComponentData
    { public Unity.Entities.BlobAssetReference<Game.Data.Blocks.BlockModulesBlob> Value; }
}
