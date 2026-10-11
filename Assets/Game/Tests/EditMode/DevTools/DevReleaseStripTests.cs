using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Game.Tests.EditMode.DevTools
{
    /// <summary>Compile actual release player scripts and inspect produced assemblies (SPEC-70 DEV-020).</summary>
    [Category("Slow")]
    public sealed class DevReleaseStripTests
    {
        string directory;
        [OneTimeSetUp] public void CompileRelease()
        {
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "tools", "release-scripts"));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            Directory.CreateDirectory(directory);
            var target = EditorUserBuildSettings.activeBuildTarget;
            var settings = new ScriptCompilationSettings { target = target, group = BuildPipeline.GetBuildTargetGroup(target), options = ScriptCompilationOptions.None };
            var result = PlayerBuildInterface.CompilePlayerScripts(settings, directory);
            Assert.IsNotNull(result.assemblies, "release script compilation failed");
            Assert.IsNotEmpty(result.assemblies, "release script compilation produced no assemblies");
        }
        [Test] public void ReleaseScripts_ContainNoDevRuntime()
        {
            string assembly = Path.Combine(directory, "Game.App.dll");
            Assert.IsTrue(File.Exists(assembly), "Game.App.dll must be present in this release check");
            byte[] bytes = File.ReadAllBytes(assembly), needle = Encoding.UTF8.GetBytes("Game.App.DevRuntime");
            for (int i = 0; i <= bytes.Length - needle.Length; i++)
            {
                int j = 0; while (j < needle.Length && bytes[i + j] == needle[j]) j++;
                if (j == needle.Length) Assert.Fail("Release Game.App.dll contains Game.App.DevRuntime; missing development guard");
            }
        }
        [Test] public void ReleaseScripts_NoDevToolsAssembly()
        {
            Assert.IsEmpty(Directory.GetFiles(directory, "Game.DevTools*.dll", SearchOption.AllDirectories), "Editor-only DevTools must not ship");
        }
    }
}
