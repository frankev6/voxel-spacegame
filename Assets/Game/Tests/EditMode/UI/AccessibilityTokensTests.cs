using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    /// <summary>UI-013: text overrides track the current approved base tokens without changing them.</summary>
    public sealed class AccessibilityTokensTests
    {
        [Test] public void ScaledTokens_MatchBaseTimesFactor()
        {
            string theme = Path.Combine(UnityEngine.Application.dataPath, "Game", "UI", "Theme");
            string baseline = File.ReadAllText(Path.Combine(theme, "tokens.uss"));
            string overrides = File.ReadAllText(Path.Combine(theme, "accessibility.uss"));
            var tokens = Regex.Matches(baseline, @"(--text-(?:xs|s|m|l|xl|xxl))\s*:\s*([0-9]+)px\s*;");
            Assert.AreEqual(6, tokens.Count);
            Check(".ui-root.text-scale-115", 1.15m); Check(".ui-root.text-scale-130", 1.30m);
            void Check(string selector, decimal factor)
            {
                var blocks = Regex.Matches(overrides, Regex.Escape(selector) + @"\s*\{([^}]*)\}");
                Assert.AreEqual(1, blocks.Count, selector);
                string block = blocks[0].Groups[1].Value;
                Assert.AreEqual(6, Regex.Matches(block, @"--text-[a-z]+\s*:").Count, selector);
                foreach (Match token in tokens)
                {
                    string key = token.Groups[1].Value;
                    var scaled = Regex.Match(block, Regex.Escape(key) + @"\s*:\s*([0-9]+)px\s*;");
                    Assert.IsTrue(scaled.Success, selector + " missing " + key);
                    decimal expected = Math.Round(decimal.Parse(token.Groups[2].Value, CultureInfo.InvariantCulture) * factor, MidpointRounding.AwayFromZero);
                    Assert.AreEqual(expected, decimal.Parse(scaled.Groups[1].Value, CultureInfo.InvariantCulture), selector + " " + key);
                }
            }
        }
    }
}
