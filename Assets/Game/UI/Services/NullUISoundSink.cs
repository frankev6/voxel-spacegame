namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §4.2. Shared UI contract.</summary>
    public sealed class NullUISoundSink : IUISoundSink { public void Play(Game.UI.Data.UISound sound) { } }
}
