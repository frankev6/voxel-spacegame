using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor.Content
{
    /// <summary>Refuses a player build when the content has errors (SPEC-04 decision 14, §5.7).</summary>
    public sealed class ContentBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport build)
        {
            if (ContentValidatorRunner.Run(out var report)) return;
            var first = string.Join("\n", report.Sorted().Where(e => e.Severity == Game.Data.Validation.ValidationSeverity.Error).Take(20)
                .Select(e => $"{e.Code} {e.AssetPath} {e.Field}: {e.Message}"));
            throw new BuildFailedException($"Content has {report.ErrorCount} errors:\n{first}");
        }
    }
}
