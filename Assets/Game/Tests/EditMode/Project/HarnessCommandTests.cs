using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Game.Tests.EditMode.Project
{
    /// <summary>DEV-002: every [CliCommand] of Game.DevTools.Editor is documented in tools/playtest/README.md with all its arguments.</summary>
    public sealed class HarnessCommandTests
    {
        [Test]
        public void EveryCliCommand_IsDocumentedInReadme()
        {
            var readme = File.ReadAllLines(Path.Combine(UnityEngine.Application.dataPath, "..", "tools", "playtest", "README.md"));
            var devTools = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "Game.DevTools.Editor");
            Assert.IsNotNull(devTools, "Game.DevTools.Editor is not loaded");
            int found = 0;
            foreach (var m in devTools.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)))
            {
                var cmd = m.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "CliCommandAttribute");
                if (cmd == null) continue;
                found++;
                string name = FirstString(cmd);
                string shortName = name.StartsWith("gh_", StringComparison.Ordinal) ? name.Substring(3) : name;
                var row = readme.FirstOrDefault(l => Regex.IsMatch(l, @"^\| `(gh_)?" + Regex.Escape(shortName) + @"[ `]"));
                Assert.IsNotNull(row, $"{name} has no row in tools/playtest/README.md");
                foreach (var p in m.GetParameters())
                {
                    var arg = p.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "CliArgAttribute");
                    string argName = arg != null ? FirstString(arg) : p.Name;
                    StringAssert.Contains("--" + argName, row, $"{name}: argument {argName} is not documented");
                }
            }
            Assert.Greater(found, 10, "expected the generic harness commands");
        }

        // The command or argument name: the first string the attribute exposes as Name, or its first constructor string.
        static string FirstString(Attribute a)
        {
            var nameProp = a.GetType().GetProperty("Name");
            if (nameProp != null && nameProp.GetValue(a) is string s && s.Length > 0) return s;
            var field = a.GetType().GetFields().FirstOrDefault(f => f.FieldType == typeof(string));
            return (string)field?.GetValue(a);
        }
    }
}
