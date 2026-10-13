namespace Game.Narrative.Dialog
{
    /// <summary>Implements SPEC-41 §4.7.</summary>
    public readonly struct DialogRequest
    {
        public readonly int BeatIndex;                    // IndexOf<StoryBeatDefinition>, 0 = debug/none
        public readonly string StartNode;
        public readonly Game.Data.Logic.LogicScope Scope; // body/quest context of the beat
        public DialogRequest(int beatIndex, string startNode, in Game.Data.Logic.LogicScope scope) { BeatIndex = beatIndex; StartNode = startNode; Scope = scope; }
    }
}
