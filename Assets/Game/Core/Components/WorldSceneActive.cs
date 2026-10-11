namespace Game.Core.Components
{
    /// <summary>Tag singleton: exists only while the World scene is ready (SPEC-01 Decision 6). Created/destroyed by SceneFlow. Implements SPEC-01 §4.1.</summary>
    public struct WorldSceneActive : Unity.Entities.IComponentData { }
}
