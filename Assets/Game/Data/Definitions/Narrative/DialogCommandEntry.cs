namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-41 §4.8.</summary>
    [System.Serializable]
    public struct DialogCommandEntry
    {
        public string Text;                                       // normalized command text, e.g. "give_item resource_fuel 3"
        public Game.Data.Logic.CompiledEffectList Effect;         // exactly one source line == Text
    }
}
