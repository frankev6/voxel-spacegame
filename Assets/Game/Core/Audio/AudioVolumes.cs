namespace Game.Core.Audio
{
    /// <summary>Linear 0..1 bus volumes, as stored by SettingsService. Implements SPEC-64 §4.2.</summary>
    public struct AudioVolumes
    {
        public float Master, Music, Sfx, Ui, Ambience;
        public static AudioVolumes Default => new AudioVolumes { Master = 0.8f, Music = 0.6f, Sfx = 0.8f, Ui = 0.7f, Ambience = 0.6f };
    }
}
