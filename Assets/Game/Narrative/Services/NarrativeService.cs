namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public sealed class NarrativeService
    {
        // TODO(NAR-010): implement this runtime service; the contract shell owns no runtime state.

        public const int MaxPassesPerTick = 8;
        public NarrativeService(FactStore facts, Game.Data.Logic.ILogicContext context) => throw new System.NotImplementedException("NAR-010");
        public FactStore Facts => throw new System.NotImplementedException("NAR-010");
        public Game.Data.Logic.ILogicContext Context => throw new System.NotImplementedException("NAR-010");
        public void Register(INarrativeSystem system) => throw new System.NotImplementedException("NAR-010");               // before BeginSession; sorted by Order
        public void BeginNewGame() => throw new System.NotImplementedException("NAR-010");
        public void OnSessionRestored() => throw new System.NotImplementedException("NAR-010");
        public void SetWorldReady(bool ready) => throw new System.NotImplementedException("NAR-010");
        public void SetPlayerFree(bool free) => throw new System.NotImplementedException("NAR-010");
        public void SetPlayerCanTalk(bool canTalk) => throw new System.NotImplementedException("NAR-010");          // set each frame by NarrativeTickBridgeSystem (§5.3)
        public bool PlayerCanTalk => throw new System.NotImplementedException("NAR-010");                   // read by NarrativeWorldEventBridge for Interact/Hail (§5.6)
        public void NotifyDependency(in Game.Data.Logic.LogicDependency dependency) => throw new System.NotImplementedException("NAR-010");   // items, credits, quests, location, time, relation
        public void Tick() => throw new System.NotImplementedException("NAR-010");
        public bool IsWorldReady => throw new System.NotImplementedException("NAR-010");
    }
}
