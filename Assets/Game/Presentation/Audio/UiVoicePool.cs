using UnityEngine;

namespace Game.Presentation.Audio
{
    /// <summary>Fixed UI voices; successful starts advance round-robin, replacing the oldest start when full.</summary>
    internal sealed class UiVoicePool
    {
        readonly AudioSource[] voices;
        internal int NextIndex { get; private set; }
        internal UiVoicePool(AudioSource[] voices) { this.voices = voices; }
        internal AudioSource Next()
        {
            var voice = voices[NextIndex]; NextIndex = (NextIndex + 1) % voices.Length;
            voice.Stop(); return voice;
        }
    }
}
