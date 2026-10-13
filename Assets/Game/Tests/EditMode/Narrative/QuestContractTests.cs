using System;
using System.IO;
using System.Linq;
using Game.Narrative.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Narrative
{
    /// <summary>SPEC-42 QST-001: configured and compiled narrative dependencies stay within Core and Data.</summary>
    public sealed class QuestContractTests
    {
        [Serializable] sealed class Asmdef
        {
            public string[] references = Array.Empty<string>();
        }

        [Test] public void NarrativeAsmdef_NoGalaxy()
        {
            string path = Path.Combine(Application.dataPath, "Game/Narrative/Game.Narrative.asmdef");
            var definition = JsonUtility.FromJson<Asmdef>(File.ReadAllText(path)); Assert.NotNull(definition);
            var assembly = typeof(QuestService).Assembly; Assert.AreEqual("Game.Narrative", assembly.GetName().Name);
            string[] allowed = { "Game.Core", "Game.Data" };
            CollectionAssert.AreEquivalent(allowed, definition.references.Where(name => name.StartsWith("Game.", StringComparison.Ordinal)));
            CollectionAssert.AreEquivalent(allowed, assembly.GetReferencedAssemblies().Select(name => name.Name).Where(name => name.StartsWith("Game.", StringComparison.Ordinal)));
            foreach (string forbidden in new[] { "Game.Galaxy", "Game.Simulation", "Game.Economy" })
            {
                CollectionAssert.DoesNotContain(definition.references, forbidden);
                Assert.IsFalse(assembly.GetReferencedAssemblies().Any(name => name.Name == forbidden), forbidden);
            }
        }
    }
}
