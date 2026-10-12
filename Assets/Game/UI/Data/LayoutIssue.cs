namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public struct LayoutIssue
    {
        public LayoutIssueKind Kind;
        public string ElementPath;    // "screen-pause/pause-resume"
        public UnityEngine.Rect Bounds;
        public UnityEngine.Rect Container;
    }
}
