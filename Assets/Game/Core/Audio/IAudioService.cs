using Unity.Mathematics;

namespace Game.Core.Audio
{
    /// <summary>Implemented by Game.Presentation AudioService; registered in Services by GameRoot (SPEC-01). Implements SPEC-64 §4.2.</summary>
    public interface IAudioService
    {
        void PlayUi(UiSound sound);                                   // 2D, Ui bus, respects cue cooldown
        void PlayCue(ContentId cue);                                  // 2D one-shot on the cue's bus
        void PlayCueAt(ContentId cue, float3 position, float volume); // spatial one-shot through the SFX pool
        void PlayStinger(ContentId cue);                              // music bus, ducks music
        void SetMusicOverride(ContentId cue, float fadeSeconds);      // scripted music; replaces the current override
        void ClearMusicOverride(float fadeSeconds);
    }
}
