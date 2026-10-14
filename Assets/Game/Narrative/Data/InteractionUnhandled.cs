using Game.Data.Logic;

namespace Game.Narrative
{
    /// <summary>SPEC-40 §5.6: an inspected body has no applicable narrative interaction.</summary>
    public readonly struct InteractionUnhandled
    {
        public readonly LogicBodyHandle Body;
        public InteractionUnhandled(LogicBodyHandle body) { Body = body; }
    }
}
