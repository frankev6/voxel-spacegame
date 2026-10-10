namespace Game.Data.Components
{
    /// <summary>ECS singleton holding the orientation tables. Created by ContentService.CreateSingletons. Implements SPEC-04 §4.7.</summary>
    public struct OrientationTablesRef : Unity.Entities.IComponentData
    { public Unity.Entities.BlobAssetReference<Game.Data.Blocks.OrientationTablesBlob> Value; }
}
