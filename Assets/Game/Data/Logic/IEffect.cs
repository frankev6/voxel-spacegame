namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface IEffect
    {
        void Apply(ILogicContext ctx);
        void Validate(Game.Data.Validation.ValidationReport report);
        void Bind(LogicBinder binder);
        bool CanApply(ILogicContext ctx);                 // pure check of this effect alone
        void AddRequirements(EffectRequirements req);     // costs, for atomic list checks (§5.4)
        EffectFlow Flow { get; }
    }
}
