using System.Linq;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>Rebuilds the content database after any import, move or delete under Assets/Game/Content (coalesced into one delayCall per
    /// editor update; deferred while in Play Mode; never reacts to the database asset itself, so rebuilds cannot loop). SPEC-04 §5.7.</summary>
    public sealed class ContentDatabasePostprocessor : AssetPostprocessor
    {
        static bool scheduled, pendingAfterPlay;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsContent)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!pendingAfterPlay) { pendingAfterPlay = true; EditorApplication.playModeStateChanged += AfterPlay; }
                return;
            }
            Schedule();
        }

        static bool IsContent(string path)
            => path.StartsWith(ContentDatabaseBuilder.ContentRoot + "/", System.StringComparison.Ordinal) && path.EndsWith(".asset", System.StringComparison.Ordinal)
               && path != ContentDatabaseBuilder.DatabasePath;

        static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += () => { scheduled = false; ContentDatabaseBuilder.Rebuild(); };
        }

        static void AfterPlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= AfterPlay;
            pendingAfterPlay = false;
            Schedule();
        }
    }
}
