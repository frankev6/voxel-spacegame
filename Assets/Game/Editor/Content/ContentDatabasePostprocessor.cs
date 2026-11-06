using System.Collections.Generic;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>Rebuilds the content database after any import, move or delete under Assets/Game/Content (coalesced into one delayCall per
    /// editor update; deferred while in Play Mode; never reacts to the database asset itself, so rebuilds cannot loop). SPEC-04 §5.7.</summary>
    public sealed class ContentDatabasePostprocessor : AssetPostprocessor
    {
        static bool scheduled, pendingAfterPlay;
        static bool fullScan;
        static readonly HashSet<string> importedPaths = new(System.StringComparer.Ordinal);

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool changed = false;
            foreach (string path in imported) if (IsContent(path)) { importedPaths.Add(path); changed = true; }
            // Deletes and moves can invalidate Unity object references before their old path
            // can be recovered; keep the full scan for these less frequent operations.
            foreach (string path in deleted) if (IsContent(path)) fullScan = changed = true;
            foreach (string path in moved) if (IsContent(path)) fullScan = changed = true;
            foreach (string path in movedFrom) if (IsContent(path)) fullScan = changed = true;
            if (!changed) return;
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
            EditorApplication.delayCall += Flush;
        }

        static void Flush()
        {
            scheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!pendingAfterPlay) { pendingAfterPlay = true; EditorApplication.playModeStateChanged += AfterPlay; }
                return;
            }
            // Clear before saving: imports raised by SaveAssetIfDirty belong to a new batch.
            var paths = new List<string>(importedPaths);
            bool rebuild = fullScan;
            importedPaths.Clear(); fullScan = false;
            if (rebuild) ContentDatabaseBuilder.Rebuild();
            else ContentDatabaseBuilder.RebuildImported(paths);
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
