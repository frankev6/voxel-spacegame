namespace Game.Data
{
    /// <summary>The only content entry point (architecture §6.2). Holds direct references; maintained by the editor builder. Implements SPEC-04 §4.9.</summary>
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Content Database", fileName = "ContentDatabase")]
    public sealed class ContentDatabase : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField] private System.Collections.Generic.List<Game.Data.Definitions.ContentDefinition> definitions = new();
        public System.Collections.Generic.IReadOnlyList<Game.Data.Definitions.ContentDefinition> Definitions => definitions;
        internal void EditorSetDefinitions(System.Collections.Generic.List<Game.Data.Definitions.ContentDefinition> sortedById) { definitions = new System.Collections.Generic.List<Game.Data.Definitions.ContentDefinition>(sortedById); } // Game.Editor + tests only
    }
}
