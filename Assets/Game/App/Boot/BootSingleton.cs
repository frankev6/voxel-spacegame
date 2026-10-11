namespace Game.App.Boot
{
    /// <summary>Tag on every entity BootSingletons creates (diagnostics only; teardown uses the snapshot, SPEC-01 Decision 5). Implements SPEC-01 §4.5.</summary>
    public struct BootSingleton : Unity.Entities.IComponentData { }
}
