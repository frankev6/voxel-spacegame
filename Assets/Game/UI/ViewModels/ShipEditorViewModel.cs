using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class ShipEditorViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title;
        [CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsCreativeChanged = new BindablePropertyChangedEventArgs(nameof(IsCreative));
        bool isCreative;
        [CreateProperty] public bool IsCreative { get => isCreative; set => Set(ref isCreative, value, IsCreativeChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ActiveToolChanged = new BindablePropertyChangedEventArgs(nameof(ActiveTool));
        int activeTool;
        [CreateProperty] public int ActiveTool { get => activeTool; set => Set(ref activeTool, value, ActiveToolChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanUndoChanged = new BindablePropertyChangedEventArgs(nameof(CanUndo));
        bool canUndo;
        [CreateProperty] public bool CanUndo { get => canUndo; set => Set(ref canUndo, value, CanUndoChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanRedoChanged = new BindablePropertyChangedEventArgs(nameof(CanRedo));
        bool canRedo;
        [CreateProperty] public bool CanRedo { get => canRedo; set => Set(ref canRedo, value, CanRedoChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs UndoLabelChanged = new BindablePropertyChangedEventArgs(nameof(UndoLabel));
        string undoLabel;
        [CreateProperty] public string UndoLabel { get => undoLabel; set => Set(ref undoLabel, value, UndoLabelChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RedoLabelChanged = new BindablePropertyChangedEventArgs(nameof(RedoLabel));
        string redoLabel;
        [CreateProperty] public string RedoLabel { get => redoLabel; set => Set(ref redoLabel, value, RedoLabelChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs MirrorXChanged = new BindablePropertyChangedEventArgs(nameof(MirrorX));
        bool mirrorX;
        [CreateProperty] public bool MirrorX { get => mirrorX; set => Set(ref mirrorX, value, MirrorXChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs MirrorYChanged = new BindablePropertyChangedEventArgs(nameof(MirrorY));
        bool mirrorY;
        [CreateProperty] public bool MirrorY { get => mirrorY; set => Set(ref mirrorY, value, MirrorYChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs MirrorZChanged = new BindablePropertyChangedEventArgs(nameof(MirrorZ));
        bool mirrorZ;
        [CreateProperty] public bool MirrorZ { get => mirrorZ; set => Set(ref mirrorZ, value, MirrorZChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RadialFoldChanged = new BindablePropertyChangedEventArgs(nameof(RadialFold));
        int radialFold;
        [CreateProperty] public int RadialFold { get => radialFold; set => Set(ref radialFold, value, RadialFoldChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SliceEnabledChanged = new BindablePropertyChangedEventArgs(nameof(SliceEnabled));
        bool sliceEnabled;
        [CreateProperty] public bool SliceEnabled { get => sliceEnabled; set => Set(ref sliceEnabled, value, SliceEnabledChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SliceLevelChanged = new BindablePropertyChangedEventArgs(nameof(SliceLevel));
        int sliceLevel;
        [CreateProperty] public int SliceLevel { get => sliceLevel; set => Set(ref sliceLevel, value, SliceLevelChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs XRayChanged = new BindablePropertyChangedEventArgs(nameof(XRay));
        bool xRay;
        [CreateProperty] public bool XRay { get => xRay; set => Set(ref xRay, value, XRayChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BoxModeChanged = new BindablePropertyChangedEventArgs(nameof(BoxMode));
        int boxMode;
        [CreateProperty] public int BoxMode { get => boxMode; set => Set(ref boxMode, value, BoxModeChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs OrientationTextChanged = new BindablePropertyChangedEventArgs(nameof(OrientationText));
        string orientationText;
        [CreateProperty] public string OrientationText { get => orientationText; set => Set(ref orientationText, value, OrientationTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs HoverTextChanged = new BindablePropertyChangedEventArgs(nameof(HoverText));
        string hoverText;
        [CreateProperty] public string HoverText { get => hoverText; set => Set(ref hoverText, value, HoverTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs StatusTextChanged = new BindablePropertyChangedEventArgs(nameof(StatusText));
        string statusText;
        [CreateProperty] public string StatusText { get => statusText; set => Set(ref statusText, value, StatusTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedCountChanged = new BindablePropertyChangedEventArgs(nameof(SelectedCount));
        int selectedCount;
        [CreateProperty] public int SelectedCount { get => selectedCount; set => Set(ref selectedCount, value, SelectedCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsDirtyChanged = new BindablePropertyChangedEventArgs(nameof(IsDirty));
        bool isDirty;
        [CreateProperty] public bool IsDirty { get => isDirty; set => Set(ref isDirty, value, IsDirtyChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanCommitChanged = new BindablePropertyChangedEventArgs(nameof(CanCommit));
        bool canCommit;
        [CreateProperty] public bool CanCommit { get => canCommit; set => Set(ref canCommit, value, CanCommitChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CommitLabelChanged = new BindablePropertyChangedEventArgs(nameof(CommitLabel));
        string commitLabel;
        [CreateProperty] public string CommitLabel { get => commitLabel; set => Set(ref commitLabel, value, CommitLabelChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CommitBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(CommitBlockedReason));
        string commitBlockedReason;
        [CreateProperty] public string CommitBlockedReason { get => commitBlockedReason; set => Set(ref commitBlockedReason, value, CommitBlockedReasonChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs PaletteChanged = new BindablePropertyChangedEventArgs(nameof(Palette));
        EditorPaletteViewModel palette;
        [CreateProperty] public EditorPaletteViewModel Palette { get => palette; set => Set(ref palette, value, PaletteChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs StatsChanged = new BindablePropertyChangedEventArgs(nameof(Stats));
        EditorStatsViewModel stats;
        [CreateProperty] public EditorStatsViewModel Stats { get => stats; set => Set(ref stats, value, StatsChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BomChanged = new BindablePropertyChangedEventArgs(nameof(Bom));
        EditorBomViewModel bom;
        [CreateProperty] public EditorBomViewModel Bom { get => bom; set => Set(ref bom, value, BomChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IssuesChanged = new BindablePropertyChangedEventArgs(nameof(Issues));
        EditorIssuesViewModel issues;
        [CreateProperty] public EditorIssuesViewModel Issues { get => issues; set => Set(ref issues, value, IssuesChanged); }
    }
}
