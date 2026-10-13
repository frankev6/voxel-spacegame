namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface ILogicContext
    {
        ILogicFacts Facts { get; }
        ILogicEconomy Economy { get; }
        ILogicNarrative Narrative { get; }
        ILogicWorld World { get; }
        LogicScope Scope { get; }
        ulong CampaignSeed { get; }
        void PushScope(in LogicScope scope);     // nested scopes allowed, max depth 8 (InvalidOperationException beyond)
        void PopScope();
    }
}
