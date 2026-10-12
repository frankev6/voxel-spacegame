namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public struct ContrastIssue
    {
        public string ElementPath;
        public UnityEngine.Color Foreground;
        public UnityEngine.Color Background;
        public float Ratio;
        public float Required;        // 4.5 or 3.0
    }
}
