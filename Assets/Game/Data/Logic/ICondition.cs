namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface ICondition
    {
        bool Evaluate(ILogicContext ctx);
        void Validate(Game.Data.Validation.ValidationReport report);   // reports with AssetPath "" (wrapper remaps, §5.6)
        void Bind(LogicBinder binder);
        void CollectDependencies(System.Collections.Generic.List<LogicDependency> into);
    }
}
