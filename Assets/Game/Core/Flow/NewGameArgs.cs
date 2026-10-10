namespace Game.Core.Flow
{
    /// <summary>Arguments for starting a new game (SPEC-02 §4.8).</summary>
    public readonly struct NewGameArgs
    {
        public readonly string CommanderName;      // trimmed; 1..32 chars (UI validates)
        public readonly ulong GalaxySeed;          // 0 = generate (SPEC-01)
        public readonly bool Creative;
        public readonly Game.Core.ContentId StartScenario;   // None = GameConfigDefinition default (SPEC-01)
        public NewGameArgs(string commanderName, ulong galaxySeed, bool creative, Game.Core.ContentId startScenario) { CommanderName = commanderName; GalaxySeed = galaxySeed; Creative = creative; StartScenario = startScenario; }
    }
}
