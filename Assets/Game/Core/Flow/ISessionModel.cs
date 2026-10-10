namespace Game.Core.Flow
{
    /// <summary>A session-scoped model with stable identity, reset in place (architecture §4.2, glossary "Session"). Implements SPEC-02 §4.8.</summary>
    public interface ISessionModel { void ResetToEmpty(); }
}
