using System;
using Game.Core;
using Game.Core.Audio;
using Game.UI.Data;
using Game.UI.Services;

namespace Game.App.Services
{
    /// <summary>Implements SPEC-60 §4.2 / Q14: configured cues, otherwise semantic UI-to-audio mapping.</summary>
    internal sealed class AppUiSoundSink : IUISoundSink
    {
        readonly IAudioService audio;
        readonly UIConfig config;
        internal AppUiSoundSink(IAudioService audio, UIConfig config) { this.audio = audio; this.config = config; }
        public void Play(UISound sound)
        {
            int index = (int)sound;
            if (config.SoundCueIds != null && index >= 0 && index < config.SoundCueIds.Length
                && !string.IsNullOrEmpty(config.SoundCueIds[index]))
            { audio.PlayCue(new ContentId(config.SoundCueIds[index])); return; }
            audio.PlayUi(sound switch
            {
                UISound.Click => UiSound.Click, UISound.Hover => UiSound.Hover,
                UISound.Open => UiSound.Open, UISound.Close => UiSound.Close,
                UISound.Error => UiSound.Error, UISound.Confirm => UiSound.Success,
                _ => throw new ArgumentOutOfRangeException(nameof(sound))
            });
        }
    }
}
