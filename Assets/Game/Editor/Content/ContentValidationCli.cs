using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>CI entry: -batchmode -executeMethod Game.Editor.Content.ContentValidationCli.Run. Exit code 1 on errors (SPEC-04 §5.7).</summary>
    public static class ContentValidationCli
    {
        public static void Run() => EditorApplication.Exit(RunAndGetExitCode());

        internal static int RunAndGetExitCode() => RunAndGetExitCode(ContentDatabaseBuilder.Rebuild());

        internal static int RunAndGetExitCode(Game.Data.ContentDatabase database)
        {
            var report = ContentValidatorRunner.Validate(database);
            System.Console.WriteLine(report.ToText());
            return report.HasErrors ? 1 : 0;
        }
    }
}
