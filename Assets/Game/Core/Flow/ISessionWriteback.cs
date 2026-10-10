namespace Game.Core.Flow
{
    /// <summary>Copies ECS-held session state into managed models before save capture or World teardown (SPEC-01 §5.6). Implements SPEC-02 §4.8.</summary>
    public interface ISessionWriteback { int WritebackOrder { get; } void WriteBackFromWorld(); }
}
