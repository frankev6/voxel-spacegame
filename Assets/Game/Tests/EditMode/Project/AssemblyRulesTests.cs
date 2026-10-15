using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Project
{
    /// <summary>
    /// Enforces docs/01-architecture.md §3: each game assembly may reference only the game assemblies listed for it.
    /// If this fails, do not edit the table below to make it pass — follow §3.3 or write an ADR.
    /// </summary>
    [Category("Project")]
    public class AssemblyRulesTests
    {
        static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>
        {
            ["Game.Core"] = new string[0],
            ["Game.Data"] = new[] { "Game.Core" },
            ["Game.Input"] = new[] { "Game.Core" },
            ["Game.Persistence"] = new[] { "Game.Core", "Game.Data" },
            ["Game.Economy"] = new[] { "Game.Core", "Game.Data" },
            ["Game.Voxel"] = new[] { "Game.Core", "Game.Data" },
            ["Game.Simulation"] = new[] { "Game.Core", "Game.Data", "Game.Voxel" },
            ["Game.Galaxy"] = new[] { "Game.Core", "Game.Data", "Game.Voxel", "Game.Simulation" },
            ["Game.Narrative"] = new[] { "Game.Core", "Game.Data" },
            ["Game.ShipEditor"] = new[] { "Game.Core", "Game.Data", "Game.Voxel", "Game.Economy" },
            ["Game.UI"] = new[] { "Game.Core", "Game.Data", "Game.Input" },
            ["Game.Presentation"] = new[] { "Game.Core", "Game.Data", "Game.Input", "Game.Voxel", "Game.Simulation", "Game.Galaxy", "Game.Narrative", "Game.Economy", "Game.UI" },
            // ADR-0116 (co-op spike branch only): the replication module sits beside Presentation, under App.
            ["Game.Net"] = new[] { "Game.Core", "Game.Data", "Game.Voxel", "Game.Simulation", "Game.Galaxy" },
            ["Game.App"] = new[] { "Game.Core", "Game.Data", "Game.Input", "Game.Persistence", "Game.Economy", "Game.Voxel", "Game.Simulation", "Game.Galaxy", "Game.Narrative", "Game.ShipEditor", "Game.UI", "Game.Presentation", "Game.Net" },
        };

        [Serializable] class AsmDef { public string name; public string[] references; public bool autoReferenced; }

        static IEnumerable<string> AsmDefPaths() =>
            Directory.GetFiles(Path.Combine(Application.dataPath, "Game"), "*.asmdef", SearchOption.AllDirectories);

        [Test]
        public void GameAssemblies_ReferenceOnlyAllowedGameAssemblies()
        {
            var problems = new List<string>();
            foreach (var path in AsmDefPaths())
            {
                var def = JsonUtility.FromJson<AsmDef>(File.ReadAllText(path));
                if (!Allowed.TryGetValue(def.name, out var allowed)) continue; // editor/test/devtools assemblies may reference everything
                foreach (var r in def.references.Where(r => r.StartsWith("Game.")))
                    if (!allowed.Contains(r)) problems.Add($"{def.name} must not reference {r}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void GameAssemblies_AreNotAutoReferenced_AndAllAreKnown()
        {
            var problems = new List<string>();
            foreach (var path in AsmDefPaths())
            {
                var def = JsonUtility.FromJson<AsmDef>(File.ReadAllText(path));
                if (def.autoReferenced) problems.Add($"{def.name}: autoReferenced must be false");
                bool known = Allowed.ContainsKey(def.name) || def.name == "Game.Editor" || def.name == "Game.DevTools.Editor" || def.name.StartsWith("Game.Tests.");
                if (!known) problems.Add($"{def.name}: unknown assembly — new assemblies need an ADR and a row in docs/01-architecture.md §3");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
