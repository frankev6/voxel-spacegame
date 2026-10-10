namespace Game.Core.Components
{
    /// <summary>Tag singleton: exists only while the World scene is ready. Created/destroyed by SceneFlow (SPEC-01). Implements SPEC-02 §4.3.</summary>
    public struct GameplayActive : Unity.Entities.IComponentData { }
}
