namespace Game.Narrative.Dialog
{
    /// <summary>One line ready to display. Implements SPEC-41 §4.6.</summary>
    public readonly struct DialogLine
    {
        public readonly string LineId; public readonly string Text;              // unescaped
        public readonly string SpeakerName;                                       // from "NAME:" prefix, "" if none
        public readonly string PortraitId; public readonly string SpeakerId; public readonly string IllustrationId;  // "" if absent
        public readonly string Node; public readonly bool IsDraft;
        public DialogLine(string lineId, string text, string speakerName, string portraitId, string speakerId, string illustrationId, string node, bool isDraft)
        { LineId = lineId; Text = text; SpeakerName = speakerName; PortraitId = portraitId; SpeakerId = speakerId; IllustrationId = illustrationId; Node = node; IsDraft = isDraft; }
    }
}
