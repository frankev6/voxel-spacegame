using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Data.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Data.Art
{
    /// <summary>ART-001 contract checks (SPEC-80 §9).</summary>
    public class ArtContractTests
    {
        /// <summary>The 44 binding token names of DESIGN-SYSTEM §10.1.</summary>
        static readonly string[] BindingTokens =
        {
            "--color-bg", "--color-surface-1", "--color-surface-2", "--color-surface-3", "--color-border", "--color-text",
            "--color-text-muted", "--color-accent", "--color-accent-strong", "--color-positive", "--color-warning",
            "--color-danger", "--color-info", "--color-ally", "--color-enemy", "--color-neutral", "--color-shield",
            "--color-hull", "--color-energy",
            "--space-1", "--space-2", "--space-3", "--space-4", "--space-5", "--space-6", "--space-7", "--space-8",
            "--radius-s", "--radius-m", "--radius-l", "--border-thin", "--border-thick",
            "--font-display", "--font-body", "--font-mono",
            "--text-xs", "--text-s", "--text-m", "--text-l", "--text-xl", "--text-xxl",
            "--duration-fast", "--duration-normal", "--duration-slow",
        };

        [Test]
        public void Definitions_DeclareIdPrefixes()
        {
            AssertPrefixes<ArtDirectionDefinition>("artdir_");
            AssertPrefixes<BrickMaterialDefinition>("brickmat_");
            AssertPrefixes<BlockVisualDefinition>("vis_");
        }

        static void AssertPrefixes<T>(params string[] expected)
        {
            var attr = (ContentIdPrefixAttribute)System.Attribute.GetCustomAttribute(typeof(T), typeof(ContentIdPrefixAttribute));
            Assert.IsNotNull(attr, $"{typeof(T).Name} has no [ContentIdPrefix]");
            CollectionAssert.AreEqual(expected, attr.Prefixes, typeof(T).Name);
        }

        [Test]
        public void TokensUss_DefinesEveryBindingTokenOnce()
        {
            Assert.AreEqual(44, BindingTokens.Length);
            Assert.AreEqual(BindingTokens.Length, BindingTokens.Distinct().Count());
            var text = File.ReadAllText(Path.Combine(Application.dataPath, "Game/UI/Theme/tokens.uss"));
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);              // strip comments
            var root = Regex.Match(text, @":root\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(root.Success, "tokens.uss has no :root block");
            var declared = Regex.Matches(root.Groups["body"].Value, @"(?<name>--[a-z0-9-]+)\s*:")
                .Cast<Match>().Select(m => m.Groups["name"].Value).ToList();
            foreach (var token in BindingTokens)
                Assert.AreEqual(1, declared.Count(d => d == token), $"{token} must be declared exactly once in :root");
        }

        [Test]
        public void BrickMaterial_DefaultsToSixtyFourLayers()
        {
            var def = ScriptableObject.CreateInstance<BrickMaterialDefinition>();
            try
            {
                Assert.AreEqual(BrickMaterialDefinition.LayerCount, def.Layers.Count);
                Assert.AreEqual(64, def.Layers.Count);
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void ArtDirection_PaletteHasThirtyTwoEntries()
        {
            var def = ScriptableObject.CreateInstance<ArtDirectionDefinition>();
            try
            {
                Assert.AreEqual(32, def.PaintPalette.Count);
                Assert.AreEqual(7, def.SpectralColors.Count);
            }
            finally { Object.DestroyImmediate(def); }
        }
    }
}
