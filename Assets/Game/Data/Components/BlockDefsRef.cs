namespace Game.Data.Components
{
    /// <summary>ECS singleton holding the block blob (architecture §6.2). Created by ContentService.CreateSingletons. Implements SPEC-04 §4.7.</summary>
    public struct BlockDefsRef : Unity.Entities.IComponentData
    { public Unity.Entities.BlobAssetReference<Game.Data.Blocks.BlockDefsBlob> Value; }
}
