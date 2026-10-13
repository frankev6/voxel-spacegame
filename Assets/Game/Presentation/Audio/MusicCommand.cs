using Game.Core;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.5: one frame's pure music decision. Change denotes a state transition.</summary>
    internal struct MusicCommand
    {
        public bool Change;
        public MusicState Target;
        public ContentHash Cue;
        public float Fade;
    }
}
