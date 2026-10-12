using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.UI.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.UI
{
    /// <summary>SPEC-60 UI-001: catalogue parity and stable dynamic element names.</summary>
    public sealed class UIContractTests
    {
        [Test] public void UIStyleClasses_AllCatalogueNames_Present()
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "../docs/specs/SPEC-60-ui-framework.md"));
            int start = text.IndexOf("### 4.6 Class catalogue", System.StringComparison.Ordinal);
            int end = text.IndexOf("### 4.7", start, System.StringComparison.Ordinal);
            var expected = new HashSet<string>();
            foreach (string row in text.Substring(start, end - start).Split('\n'))
            {
                if (!row.StartsWith("| ", System.StringComparison.Ordinal)) continue;
                string[] cells = row.Split('|');
                var blockMatch = Regex.Match(cells[1], @"`\.([a-z0-9_-]+)`");
                string block = blockMatch.Success ? blockMatch.Groups[1].Value : "";
                for (int column = 1; column <= 3; column++)
                    foreach (Match match in Regex.Matches(cells[column], @"`(\.[a-z0-9_-]+|__[a-z0-9_-]+|--[a-z0-9_-]+)`"))
                    {
                        string token = match.Groups[1].Value;
                        expected.Add(token[0] == '.' ? token.Substring(1) : block + token);
                    }
                if (row.Contains("…")) foreach (string size in new[] { "xs", "s", "m", "l", "xl", "xxl" }) expected.Add("text-" + size);
            }
            var actual = new List<string>();
            foreach (var field in typeof(UIStyleClasses).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                Assert.IsTrue(field.IsLiteral && field.FieldType == typeof(string), field.Name);
                actual.Add((string)field.GetRawConstantValue());
            }
            CollectionAssert.AreEquivalent(expected, actual);
        }
        [Test] public void UINames_Item_FormatsKebab()
            => Assert.AreEqual("inventory-slot-12", UINames.Item("inventory", "slot", "12"));
    }
}
