namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface IIntExpr
    {
        int Evaluate(ILogicContext ctx);
        void Validate(Game.Data.Validation.ValidationReport report);
        void Bind(LogicBinder binder);
        void CollectDependencies(System.Collections.Generic.List<LogicDependency> into);
    }
}
