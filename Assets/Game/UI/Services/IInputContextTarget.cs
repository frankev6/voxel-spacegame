namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §4.2. Shared UI contract.</summary>
    public interface IInputContextTarget { void SetContext(Game.Core.InputContext context); }
}
