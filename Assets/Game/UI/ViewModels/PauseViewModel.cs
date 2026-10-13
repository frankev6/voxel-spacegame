using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class PauseViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanSaveChanged = new BindablePropertyChangedEventArgs(nameof(CanSave));
        bool canSave;
        [CreateProperty] public bool CanSave { get => canSave; set => Set(ref canSave, value, CanSaveChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SaveBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(SaveBlockedReason));
        string saveBlockedReason;
        [CreateProperty] public string SaveBlockedReason { get => saveBlockedReason; set => Set(ref saveBlockedReason, value, SaveBlockedReasonChanged); }
    }
}
