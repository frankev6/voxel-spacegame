using Game.Core.Settings;

namespace Game.App.Settings
{
    /// <summary>settings.json: a version, then one object per section (SPEC-01 §5.6). A missing section keeps its defaults.</summary>
    public sealed class GameSettingsDto
    {
        public int version = GameSettings.CurrentVersion;
        public AudioSettingsData audio;
        public VideoSettingsData video;
        public ControlSettingsData controls;
        public GameplaySettingsData gameplay;
        public AccessibilitySettingsData accessibility;

        public static GameSettingsDto From(GameSettings s) => new GameSettingsDto
        { audio = s.Audio, video = s.Video, controls = s.Controls, gameplay = s.Gameplay, accessibility = s.Accessibility };

        public void CopyInto(GameSettings s)
        {
            if (audio != null) s.Audio.CopyFrom(audio);
            if (video != null) s.Video.CopyFrom(video);
            if (controls != null) s.Controls.CopyFrom(controls);
            if (gameplay != null) s.Gameplay.CopyFrom(gameplay);
            if (accessibility != null) s.Accessibility.CopyFrom(accessibility);
        }
    }
}
