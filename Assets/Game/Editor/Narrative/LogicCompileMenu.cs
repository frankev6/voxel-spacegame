using Game.Editor.Content;
using UnityEditor;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 NAR-009: batch compile using the same owner/stamp path as imports.</summary>
    public static class LogicCompileMenu
    {
        [MenuItem("Game/Narrative/Recompile All Logic")]
        public static void RecompileAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { LogicCompilePostprocessor.RecompileAll(null); return; }
            var report = LogicCompilePostprocessor.RecompileAll(ContentDatabaseBuilder.Rebuild());
            if (report != null) ContentValidatorRunner.Log(report);
        }
        [MenuItem("Game/Narrative/Recompile All Logic", true)]
        static bool CanRecompile() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
