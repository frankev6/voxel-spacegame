using Game.Core;

namespace Game.Core.Events
{
    /// <summary>The active input context changed (SPEC-02 §4.5).</summary>
    public readonly struct InputContextChanged { public readonly InputContext Previous; public readonly InputContext Current; public InputContextChanged(InputContext previous, InputContext current) { Previous = previous; Current = current; } }
}
