using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Project
{
    /// <summary>
    /// Mechanical checks for docs/02-conventions.md over runtime game code (Editor, DevTools and Tests are exempt).
    /// A line may opt out with a trailing comment "// conventions-ok: &lt;reason&gt;" — reviewers must agree with the reason.
    /// </summary>
    [Category("Project")]
    public class ConventionsTests
    {
        static readonly (string rule, Regex pattern)[] Rules =
        {
            ("no UnityEngine.Random (use RngStreams)", new Regex(@"\bUnityEngine\.Random\b|(?<![\w.])Random\.(Range|value|insideUnitSphere|onUnitSphere|InitState)\b")),
            ("no System.Random (use RngStreams)", new Regex(@"\bnew\s+(System\.)?Random\s*\(")),
            ("no Resources.Load (use ContentDatabase/Services)", new Regex(@"\bResources\.Load")),
            ("no Find* lookups (use Services)", new Regex(@"\b(FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType)\b|\bGameObject\.Find\(")),
            ("no managed IComponentData (Entities 6.6)", new Regex(@"\bclass\s+\w+\s*:\s*[^{]*\bIComponentData\b")),
            ("no bare Debug.Log (use GameLog)", new Regex(@"\bDebug\.Log(Warning|Error)?\s*\(")),
            ("TODO needs a task ID: TODO(ABC-123)", new Regex(@"\bTODO\b(?!\([A-Z]{2,4}-\d{3}\))")),
            ("never reference placeholder art from code", new Regex(@"_Placeholder")),
            ("Time.timeScale is written only by PauseService (SPEC-01 decision 10)", new Regex(@"\bTime\.timeScale\s*=(?!=)")),
            ("device state is read only through InputService (SPEC-03 decision 1)", new Regex(@"\b(Keyboard|Mouse|Gamepad)\.current\b")),
        };

        static IEnumerable<string> RuntimeSources()
        {
            var root = Path.Combine(Application.dataPath, "Game");
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Tests/") && !p.Replace('\\', '/').Contains("/Editor/") &&
                            !p.Replace('\\', '/').Contains("/DevTools/"));
        }

        [Test]
        public void RuntimeCode_FollowsMechanicalConventions()
        {
            var problems = new List<string>();
            foreach (var file in RuntimeSources())
            {
                var lines = File.ReadAllLines(file);
                bool isGameLog = Path.GetFileName(file) == "GameLog.cs";
                bool isBootstrap = Path.GetFileName(file) == "Bootstrap.cs";
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.Contains("conventions-ok:")) continue;
                    var code = Regex.Replace(line, @"^\s*//.*$", m => line.Contains("TODO") ? line : ""); // comments only matter for TODO
                    foreach (var (rule, pattern) in Rules)
                    {
                        if (isGameLog && rule.StartsWith("no bare Debug.Log")) continue;
                        if (isBootstrap && rule.StartsWith("no Resources.Load")) continue;
                        if (Path.GetFileName(file) == "PauseService.cs" && rule.StartsWith("Time.timeScale")) continue;
                        if (rule.StartsWith("device state") && (file.Replace('\\', '/').Contains("/Game/Input/") ||
                            Path.GetFileName(file) == "PlayerInputSampleSystem.cs" || Path.GetFileName(file) == "EditorInputSampler.cs")) continue;
                        if (pattern.IsMatch(code)) problems.Add($"{Rel(file)}:{i + 1}: {rule}\n    {line.Trim()}");
                    }
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        static string Rel(string p) => "Assets" + p.Replace('\\', '/').Substring(Application.dataPath.Length);
    }
}
