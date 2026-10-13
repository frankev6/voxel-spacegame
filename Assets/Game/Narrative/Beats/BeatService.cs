namespace Game.Narrative.Beats
{
    /// <summary>SPEC-42 §5.2 beat service contract; runtime behavior follows in QST-005.</summary>
    public sealed class BeatService : Game.Narrative.INarrativeSystem, System.IDisposable
    {
        public BeatService(Game.Data.Services.ContentCatalog catalog, FactStore facts, Game.Data.Logic.ILogicContext context,
                           Game.Narrative.Dialog.DialogService dialog, System.Func<Game.Core.ContentHash, uint> authoredStarSystemValue,
                           System.Func<Game.Core.ContentHash, string> bodyTagOfTemplate, ulong campaignSeed) => throw new System.NotImplementedException("QST-005");
        public int Order => 30;
        // Triggers (called by the App bridge, §5.6)
        public bool OnBodyInteracted(in Game.Data.Logic.LogicBodyHandle body, bool playerCanTalk) => throw new System.NotImplementedException("QST-005");   // true = a beat was started or queued
        public bool OnHail(Game.Core.ContentHash encounter, ulong memberPersistentId, bool playerCanTalk) => throw new System.NotImplementedException("QST-005");
        public void OnStarSystemEntered(uint starSystemValue, bool firstVisit) => throw new System.NotImplementedException("QST-005");
        // Effects (through NarrativeLogicAdapter)
        public void Queue(int beatIndex) => throw new System.NotImplementedException("QST-005");                                        // queue_beat
        public void CompleteBeat(int beatIndex) => throw new System.NotImplementedException("QST-005");                                 // complete_beat (nested)
        public void AbortCurrent() => throw new System.NotImplementedException("QST-005");                                              // abort_beat
        // State
        public int CurrentBeatIndex => throw new System.NotImplementedException("QST-005");                                     // 0 = none
        public int QueuedCount => throw new System.NotImplementedException("QST-005");
        public void DebugPlay(int beatIndex) => throw new System.NotImplementedException("QST-005");                                    // Narrative Debugger (SPEC-40 §5.9), ignores conditions and repeat
        public void Dispose() => throw new System.NotImplementedException("QST-005");                                                   // unsubscribes from DialogService.Ended
        // TODO(QST-005): implement the canonical narrative lifecycle and owned runtime behavior.
        public void OnNewGame() => throw new System.NotImplementedException("QST-005");
        public void OnRestored() => throw new System.NotImplementedException("QST-005");
        public void Evaluate(System.Collections.Generic.IReadOnlyList<Game.Data.Logic.LogicDependency> changed, bool fullReevaluate)
            => throw new System.NotImplementedException("QST-005");
        public void AfterEvaluation(bool playerFree) => throw new System.NotImplementedException("QST-005");
    }
}
