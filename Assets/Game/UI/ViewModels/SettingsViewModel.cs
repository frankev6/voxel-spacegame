using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class SettingsViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs SelectedTabChanged = new BindablePropertyChangedEventArgs(nameof(SelectedTab));
        int selectedTab;
        [CreateProperty] public int SelectedTab { get => selectedTab; set => Set(ref selectedTab, value, SelectedTabChanged); }

        static readonly BindablePropertyChangedEventArgs MasterVolumeChanged = new BindablePropertyChangedEventArgs(nameof(MasterVolume));
        float masterVolume;
        [CreateProperty] public float MasterVolume { get => masterVolume; set => Set(ref masterVolume, value, MasterVolumeChanged); }

        static readonly BindablePropertyChangedEventArgs MusicVolumeChanged = new BindablePropertyChangedEventArgs(nameof(MusicVolume));
        float musicVolume;
        [CreateProperty] public float MusicVolume { get => musicVolume; set => Set(ref musicVolume, value, MusicVolumeChanged); }

        static readonly BindablePropertyChangedEventArgs SfxVolumeChanged = new BindablePropertyChangedEventArgs(nameof(SfxVolume));
        float sfxVolume;
        [CreateProperty] public float SfxVolume { get => sfxVolume; set => Set(ref sfxVolume, value, SfxVolumeChanged); }

        static readonly BindablePropertyChangedEventArgs UiVolumeChanged = new BindablePropertyChangedEventArgs(nameof(UiVolume));
        float uiVolume;
        [CreateProperty] public float UiVolume { get => uiVolume; set => Set(ref uiVolume, value, UiVolumeChanged); }

        static readonly BindablePropertyChangedEventArgs ResolutionsChanged = new BindablePropertyChangedEventArgs(nameof(Resolutions));
        [CreateProperty] public List<string> Resolutions { get; } = new List<string>(32);
        public void NotifyResolutions() => Notify(ResolutionsChanged);

        static readonly BindablePropertyChangedEventArgs ResolutionIndexChanged = new BindablePropertyChangedEventArgs(nameof(ResolutionIndex));
        int resolutionIndex;
        [CreateProperty] public int ResolutionIndex { get => resolutionIndex; set => Set(ref resolutionIndex, value, ResolutionIndexChanged); }

        static readonly BindablePropertyChangedEventArgs FullscreenModeIndexChanged = new BindablePropertyChangedEventArgs(nameof(FullscreenModeIndex));
        int fullscreenModeIndex;
        [CreateProperty] public int FullscreenModeIndex { get => fullscreenModeIndex; set => Set(ref fullscreenModeIndex, value, FullscreenModeIndexChanged); }

        static readonly BindablePropertyChangedEventArgs VSyncChanged = new BindablePropertyChangedEventArgs(nameof(VSync));
        bool vSync;
        [CreateProperty] public bool VSync { get => vSync; set => Set(ref vSync, value, VSyncChanged); }

        static readonly BindablePropertyChangedEventArgs QualityNamesChanged = new BindablePropertyChangedEventArgs(nameof(QualityNames));
        [CreateProperty] public List<string> QualityNames { get; } = new List<string>(8);
        public void NotifyQualityNames() => Notify(QualityNamesChanged);

        static readonly BindablePropertyChangedEventArgs QualityIndexChanged = new BindablePropertyChangedEventArgs(nameof(QualityIndex));
        int qualityIndex;
        [CreateProperty] public int QualityIndex { get => qualityIndex; set => Set(ref qualityIndex, value, QualityIndexChanged); }

        static readonly BindablePropertyChangedEventArgs MouseSensitivityChanged = new BindablePropertyChangedEventArgs(nameof(MouseSensitivity));
        float mouseSensitivity;
        [CreateProperty] public float MouseSensitivity { get => mouseSensitivity; set => Set(ref mouseSensitivity, value, MouseSensitivityChanged); }

        static readonly BindablePropertyChangedEventArgs InvertOrbitYChanged = new BindablePropertyChangedEventArgs(nameof(InvertOrbitY));
        bool invertOrbitY;
        [CreateProperty] public bool InvertOrbitY { get => invertOrbitY; set => Set(ref invertOrbitY, value, InvertOrbitYChanged); }

        static readonly BindablePropertyChangedEventArgs RebindsChanged = new BindablePropertyChangedEventArgs(nameof(Rebinds));
        [CreateProperty] public List<RebindRowViewModel> Rebinds { get; } = new List<RebindRowViewModel>(64);
        public void NotifyRebinds() => Notify(RebindsChanged);

        static readonly BindablePropertyChangedEventArgs ListeningRowChanged = new BindablePropertyChangedEventArgs(nameof(ListeningRow));
        int listeningRow = -1;
        [CreateProperty] public int ListeningRow { get => listeningRow; set => Set(ref listeningRow, value, ListeningRowChanged); }

        static readonly BindablePropertyChangedEventArgs CameraYawModeIndexChanged = new BindablePropertyChangedEventArgs(nameof(CameraYawModeIndex));
        int cameraYawModeIndex;
        [CreateProperty] public int CameraYawModeIndex { get => cameraYawModeIndex; set => Set(ref cameraYawModeIndex, value, CameraYawModeIndexChanged); }

        static readonly BindablePropertyChangedEventArgs ScreenShakeChanged = new BindablePropertyChangedEventArgs(nameof(ScreenShake));
        float screenShake;
        [CreateProperty] public float ScreenShake { get => screenShake; set => Set(ref screenShake, value, ScreenShakeChanged); }

        static readonly BindablePropertyChangedEventArgs UiScaleChanged = new BindablePropertyChangedEventArgs(nameof(UiScale));
        float uiScale;
        [CreateProperty] public float UiScale { get => uiScale; set => Set(ref uiScale, value, UiScaleChanged); }

        static readonly BindablePropertyChangedEventArgs TextScaleIndexChanged = new BindablePropertyChangedEventArgs(nameof(TextScaleIndex));
        int textScaleIndex;
        [CreateProperty] public int TextScaleIndex { get => textScaleIndex; set => Set(ref textScaleIndex, value, TextScaleIndexChanged); }

        static readonly BindablePropertyChangedEventArgs ReduceMotionChanged = new BindablePropertyChangedEventArgs(nameof(ReduceMotion));
        bool reduceMotion;
        [CreateProperty] public bool ReduceMotion { get => reduceMotion; set => Set(ref reduceMotion, value, ReduceMotionChanged); }
    }
}
