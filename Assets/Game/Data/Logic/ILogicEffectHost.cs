namespace Game.Data.Logic
{
    /// <summary>Optional owner hooks for effect tracing and blocking dialog continuation (ADR-0075).</summary>
    public interface ILogicEffectHost
    {
        System.Action BlockingCommandCompleted { get; }
        void EffectRejected();
    }
}
