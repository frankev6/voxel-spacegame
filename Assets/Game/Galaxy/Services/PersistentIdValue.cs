namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4. Persistent ship identity, never an ECS entity in managed state.</summary>
    public readonly struct PersistentIdValue { public readonly ulong Value; public PersistentIdValue(ulong v) { Value = v; } }
}
