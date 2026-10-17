using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor.Build
{
    /// <summary>SPEC-90 NET-002: a standalone development player for a second co-op instance. Run through tools/build-dev.sh.</summary>
    public static class DevBuild
    {
        /// <summary>From an open Editor (CLI: invoke --type Game.Editor.Build.DevBuild --method BuildMacDev). Returns the summary line.</summary>
        public static string BuildMacDev()
        {
            var report = Build(out string output);
            var summary = report.summary;
            return "DevBuild: " + summary.result + ", " + summary.totalErrors + " errors, " + summary.totalTime.TotalSeconds.ToString("0") + " s -> " + output;
        }

        static BuildReport Build(out string output)
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "dev", "SpaceshipGame.app"));
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            return BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
        }

        /// <summary>RACE-010: the Windows 64-bit development player for remote co-op playtests (Builds/windows/SpaceshipGame.exe).
        /// Needs Unity Hub's "Windows Build Support (Mono)" module. Run through tools/build-windows.sh.</summary>
        public static void BuildWindowsDevAndQuit()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "windows", "SpaceshipGame.exe"));
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Debug.LogError("DevBuild: Windows Build Support is not installed. Unity Hub → Installs → this version → Add modules → Windows Build Support (Mono).");
                EditorApplication.Exit(2); return;
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                { scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            var summary = report.summary;
            Debug.Log("DevBuild: " + summary.result + ", " + summary.totalErrors + " errors, " + (summary.totalSize / (1024 * 1024)) + " MB, "
                + summary.totalTime.TotalSeconds.ToString("0") + " s -> " + output);
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void BuildMacDevAndQuit()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "dev", "SpaceshipGame.app"));
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            var summary = report.summary;
            Debug.Log("DevBuild: " + summary.result + ", " + summary.totalErrors + " errors, " + (summary.totalSize / (1024 * 1024)) + " MB, "
                + summary.totalTime.TotalSeconds.ToString("0") + " s -> " + output);
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
