namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public interface INarrativeSystem
    {
        int Order { get; }                                           // ascending
        void OnNewGame();
        void OnRestored();                                           // after every chunk restored; mark all dirty; MUST NOT apply effects
        void Evaluate(System.Collections.Generic.IReadOnlyList<Game.Data.Logic.LogicDependency> changed, bool fullReevaluate);
        void AfterEvaluation(bool playerFree);                       // queue pumping, marker rebuild
    }
}
