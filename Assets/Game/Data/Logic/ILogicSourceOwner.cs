namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.5); behavior follows in the named implementation task.</summary>
    public interface ILogicSourceOwner
    {
        void CompileLogic(Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope);  // compile every field
        void BindLogic(LogicBinder binder);                                                             // bind every field
    }
}
