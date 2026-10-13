using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Dialog screen data (SPEC-41 §4.9), preserving SPEC-61 §4.2's staged properties.</summary>
    public sealed class DialogViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SpeakerNameChanged = new BindablePropertyChangedEventArgs(nameof(SpeakerName));
        string speakerName;
        [CreateProperty] public string SpeakerName { get => speakerName; set => Set(ref speakerName, value, SpeakerNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs PortraitChanged = new BindablePropertyChangedEventArgs(nameof(Portrait));
        UnityEngine.Sprite portrait;
        [CreateProperty] public UnityEngine.Sprite Portrait { get => portrait; set => Set(ref portrait, value, PortraitChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs HasPortraitChanged = new BindablePropertyChangedEventArgs(nameof(HasPortrait));
        bool hasPortrait;
        [CreateProperty] public bool HasPortrait { get => hasPortrait; set => Set(ref hasPortrait, value, HasPortraitChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs LineTextChanged = new BindablePropertyChangedEventArgs(nameof(LineText));
        string lineText;
        [CreateProperty] public string LineText { get => lineText; set => Set(ref lineText, value, LineTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs LineVersionChanged = new BindablePropertyChangedEventArgs(nameof(LineVersion));
        int lineVersion;
        [CreateProperty] public int LineVersion { get => lineVersion; set => Set(ref lineVersion, value, LineVersionChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs OptionsChanged = new BindablePropertyChangedEventArgs(nameof(Options));
        [CreateProperty] public List<DialogOptionViewModel> Options { get; } = new List<DialogOptionViewModel>(9);
        public void NotifyOptions() => Notify(OptionsChanged);
        public void NotifyOptionsChanged() => NotifyOptions();

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs LinesChanged = new BindablePropertyChangedEventArgs(nameof(Lines));
        [CreateProperty] public List<DialogLineViewModel> Lines { get; } = new List<DialogLineViewModel>(32);
        public void NotifyLinesChanged() => Notify(LinesChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IllustrationChanged = new BindablePropertyChangedEventArgs(nameof(Illustration));
        UnityEngine.Sprite illustration;
        [CreateProperty] public UnityEngine.Sprite Illustration { get => illustration; set => Set(ref illustration, value, IllustrationChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs HasIllustrationChanged = new BindablePropertyChangedEventArgs(nameof(HasIllustration));
        bool hasIllustration;
        [CreateProperty] public bool HasIllustration { get => hasIllustration; set => Set(ref hasIllustration, value, HasIllustrationChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ScrollToLineChanged = new BindablePropertyChangedEventArgs(nameof(ScrollToLine));
        int scrollToLine;
        [CreateProperty] public int ScrollToLine { get => scrollToLine; set => Set(ref scrollToLine, value, ScrollToLineChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanContinueChanged = new BindablePropertyChangedEventArgs(nameof(CanContinue));
        bool canContinue;
        [CreateProperty] public bool CanContinue { get => canContinue; set => Set(ref canContinue, value, CanContinueChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsWaitingForChoiceChanged = new BindablePropertyChangedEventArgs(nameof(IsWaitingForChoice));
        bool isWaitingForChoice;
        [CreateProperty] public bool IsWaitingForChoice { get => isWaitingForChoice; set => Set(ref isWaitingForChoice, value, IsWaitingForChoiceChanged); }
    }
}
