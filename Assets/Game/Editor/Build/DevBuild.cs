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
