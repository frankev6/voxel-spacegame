using System;
using System.IO;
using System.Linq;
using Game.Data.Definitions;
using Game.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Economy
{
    /// <summary>SPEC-50 ECO-001: declared/compiled module boundaries and the exact category bit contract.</summary>
    public sealed class EconomyContractTests
    {
        [Serializable] sealed class Asmdef
        {
            public string name = "";
            public string[] references = Array.Empty<string>();
        }

        [Test] public void EconomyAsmdef_ReferencesCoreDataOnly()
        {
            var definition = JsonUtility.FromJson<Asmdef>(File.ReadAllText("Assets/Game/Economy/Game.Economy.asmdef"));
            Assert.AreEqual("Game.Economy", definition.name);
            CollectionAssert.AreEquivalent(new[] { "Game.Core", "Game.Data" }, definition.references.Where(r => r.StartsWith("Game.", StringComparison.Ordinal)));
            // Architecture §3 lists game-module references; preserve already allowed shared package references (Q8).
            foreach (string reference in definition.references)
                Assert.IsTrue(reference == "Game.Core" || reference == "Game.Data" || reference == "Unity.Collections" || reference == "Unity.Mathematics", reference);
            var compiled = typeof(InventoryModel).Assembly.GetReferencedAssemblies();
            CollectionAssert.AreEquivalent(new[] { "Game.Core", "Game.Data" }, compiled.Where(a => a.Name.StartsWith("Game.", StringComparison.Ordinal)).Select(a => a.Name));
            Assert.IsFalse(compiled.Any(a => a.Name.StartsWith("Unity.Entities", StringComparison.Ordinal)), "Economy must remain pure managed code.");
        }

        [Test] public void CategoryMask_MatchesEnum()
        {
            Assert.AreEqual(typeof(ushort), Enum.GetUnderlyingType(typeof(ItemCategoryMask)));
            Assert.AreEqual(0, (int)ItemCategoryMask.None);
            var categories = (ItemCategory[])Enum.GetValues(typeof(ItemCategory));
            Assert.AreEqual(categories.Length + 1, Enum.GetValues(typeof(ItemCategoryMask)).Length);
            foreach (var category in categories)
            {
                Assert.IsTrue(Enum.TryParse(category.ToString(), out ItemCategoryMask mask), category.ToString());
                Assert.AreEqual(1 << (int)category, (int)mask, category.ToString());
            }
        }
    }
}
