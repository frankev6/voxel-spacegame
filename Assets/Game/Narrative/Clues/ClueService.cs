namespace Game.Narrative.Clues
{
    /// <summary>SPEC-42 §5.4 clue, rumor and mystery service contract; behavior follows in QST-004.</summary>
    public sealed class ClueService : Game.Narrative.INarrativeSystem
    {
        public ClueService(Game.Data.Services.ContentCatalog catalog, FactStore facts, Game.Data.Logic.ILogicContext context) => throw new System.NotImplementedException("QST-004");
        public int Order => 20;
        public bool Reveal(int clueIndex) => throw new System.NotImplementedException("QST-004");                                       // true when newly revealed
        public bool IsKnown(int clueIndex) => throw new System.NotImplementedException("QST-004");
        public int OrdinalOf(int clueIndex) => throw new System.NotImplementedException("QST-004");                                     // 0 = unknown
        public bool IsResolved(int rumorClueIndex) => throw new System.NotImplementedException("QST-004");                              // rumors only
        public bool IsSolved(int mysteryIndex) => throw new System.NotImplementedException("QST-004");
        public bool IsSeen(int clueIndex) => throw new System.NotImplementedException("QST-004"); public void MarkSeen(int clueIndex) => throw new System.NotImplementedException("QST-004");  // ship-log "new" badge
        public event System.Action<int> ClueChanged
        {
            add => throw new System.NotImplementedException("QST-004");
            remove => throw new System.NotImplementedException("QST-004");
        }                             // clue index (revealed, resolved, seen)
        public event System.Action<int> MysterySolved
        {
            add => throw new System.NotImplementedException("QST-004");
            remove => throw new System.NotImplementedException("QST-004");
        }                           // mystery index
        // TODO(QST-004): implement the canonical narrative lifecycle and owned runtime behavior.
        public void OnNewGame() => throw new System.NotImplementedException("QST-004");
        public void OnRestored() => throw new System.NotImplementedException("QST-004");
        public void Evaluate(System.Collections.Generic.IReadOnlyList<Game.Data.Logic.LogicDependency> changed, bool fullReevaluate)
            => throw new System.NotImplementedException("QST-004");
        public void AfterEvaluation(bool playerFree) => throw new System.NotImplementedException("QST-004");
    }
}
