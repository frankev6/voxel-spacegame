using System.Text.RegularExpressions;
using Game.Core;
using Game.Data.Validation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Data
{
    public sealed class ValidationReportTests
    {
        const string Category = "ValidationReportTests";

        static readonly (ValidationSeverity severity, string code, string path, string field, string message)[] Fixture =
        {
            (ValidationSeverity.Error, "DATA-E004", "Assets/Game/Content/Blocks/block_hull.asset", "id", "Duplicate ID block_hull."),
            (ValidationSeverity.Warning, "DATA-W023", "Assets/Game/Content/Items/item_ore.asset", "stackSize", "Stack size is unusually large."),
            (ValidationSeverity.Info, "DATA-I001", "Assets/Game/Content/Items/item_ore.asset", "", "Item has no icon yet."),
            (ValidationSeverity.Error, "DATA-E030", "Assets/Game/Content/Blocks/block_hull.asset", "footprint", "Footprint is empty."),
            (ValidationSeverity.Error, "DATA-E004", "Assets/Game/Content/Blocks/block_armor.asset", "id", "Duplicate ID block_armor."),
        };

        static ValidationReport Build(params int[] order)
        {
            var report = new ValidationReport("Test");
            foreach (int i in order)
            {
                var f = Fixture[i];
                report.Add(f.severity, f.code, f.path, f.field, f.message);
            }
            return report;
        }

        [Test]
        public void Counts_MatchSeverities()
        {
            var report = Build(0, 1, 2, 3, 4);
            Assert.AreEqual(3, report.ErrorCount);
            Assert.AreEqual(1, report.WarningCount);
            Assert.AreEqual(1, report.InfoCount);
            Assert.AreEqual(5, report.Entries.Count);
            Assert.IsTrue(report.HasErrors);

            var scoped = new ValidationReport("Scoped");
            var scope = scoped.For("save:p001/manual_001");
            scope.Warning("PERS-W001", "header", "Old version.");
            scope.Info("PERS-I001", "", "Migrated.");
            Assert.IsFalse(scoped.HasErrors);
            Assert.AreEqual(1, scoped.WarningCount);
            Assert.AreEqual(1, scoped.InfoCount);
            Assert.AreEqual("save:p001/manual_001", scoped.Entries[0].AssetPath);
        }

        [Test]
        public void ToText_IsOrderIndependent()
        {
            string a = Build(0, 1, 2, 3, 4).ToText();
            string b = Build(4, 3, 2, 1, 0).ToText();
            string c = Build(2, 0, 4, 1, 3).ToText();
            Assert.AreEqual(a, b);
            Assert.AreEqual(a, c);
            StringAssert.StartsWith("[E] DATA-E004 Assets/Game/Content/Blocks/block_armor.asset id: Duplicate ID block_armor.\n", a);
            StringAssert.Contains("[I] DATA-I001 Assets/Game/Content/Items/item_ore.asset: Item has no icon yet.\n", a);
            StringAssert.EndsWith("3 errors, 1 warnings, 1 infos", a);

            string capped = Build(0, 1, 2, 3, 4).ToText(2);
            Assert.AreEqual(4, capped.Split('\n').Length);   // 2 entries, "... 3 more", summary
            StringAssert.Contains("... 3 more\n", capped);
        }

        [Test]
        public void Merge_CombinesEntries()
        {
            var first = Build(0, 1);
            var second = Build(2, 3, 4);
            first.Merge(second);
            Assert.AreEqual(5, first.Entries.Count);
            Assert.AreEqual(3, first.ErrorCount);
            Assert.AreEqual(1, first.WarningCount);
            Assert.AreEqual(1, first.InfoCount);
            Assert.AreEqual(Build(0, 1, 2, 3, 4).ToText(), first.ToText());
            Assert.AreEqual(3, second.Entries.Count, "Merge must not change the source report");
        }

        [Test]
        public void LogTo_CapsLines()
        {
            GameLog.SetMinimumLevel(Category, LogLevel.Info);
            var report = new ValidationReport("Big");
            for (int i = 0; i < 80; i++) report.Warning("DATA-W001", $"Assets/w{i:D3}.asset", "", "Warning.");
            for (int i = 0; i < 20; i++) report.Error("DATA-E001", $"Assets/e{i:D3}.asset", "", "Error.");

            for (int i = 0; i < 20; i++) LogAssert.Expect(LogType.Error, new Regex(@"^\[ValidationReportTests\] \[E\] DATA-E001"));
            for (int i = 0; i < 30; i++) LogAssert.Expect(LogType.Warning, new Regex(@"^\[ValidationReportTests\] \[W\] DATA-W001"));
            LogAssert.Expect(LogType.Error, "[ValidationReportTests] Big: 20 errors, 80 warnings, 0 infos (50 not shown)");
            report.LogTo(Category, 50);
            LogAssert.NoUnexpectedReceived();

            var clean = new ValidationReport("Clean");
            LogAssert.Expect(LogType.Log, "[ValidationReportTests] Clean: 0 errors, 0 warnings, 0 infos");
            clean.LogTo(Category);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Exception_CarriesReport()
        {
            var report = Build(0);
            var e = new ContentValidationException(report);
            Assert.AreSame(report, e.Report);
            StringAssert.Contains("DATA-E004", e.Message);
        }
    }
}
