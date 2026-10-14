using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.App.Services;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Narrative.Dialog;
using Game.Narrative;
using Game.Narrative.Dialog;
using Game.Narrative.ShipLog;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class DialogRunnerConformanceTests
    {
        sealed class Host : IDialogRunnerHost
        {
            public readonly List<string> Lines = new(), Started = new(), Completed = new(), Commands = new();
            public DialogOption[] Options = Array.Empty<DialogOption>();
            public bool AtLine, Ended;
            public Func<string, DialogCommandResult> Command;
            public void OnLine(in DialogLine line) { Lines.Add(line.Text); AtLine = true; }
            public void OnOptions(DialogOption[] options, int count) { Options = options.Take(count).ToArray(); AtLine = false; }
            public DialogCommandResult OnCommand(string text) { Commands.Add(text); AtLine = false; return Command?.Invoke(text) ?? DialogCommandResult.Continue; }
            public void OnNodeStarted(string node) => Started.Add(node);
            public void OnNodeCompleted(string node) => Completed.Add(node);
            public void OnDialogComplete() { Ended = true; AtLine = false; }
        }
        sealed class Fixture : IDisposable
        {
            readonly LogicTestEnvironment environment = new();
            public readonly DialogProgramDefinition Program;
            public readonly IDialogRunner Runner;
            public readonly DialogMemory Memory = new();
            public readonly FactStore Facts;
            public readonly Host Host = new();
            public Fixture(string source)
            {
                var report = new ValidationReport("runner conformance");
                Program = DialogProgramCompiler.Compile(new Dictionary<string,string> { ["test.yarn"] = source }, environment.Content, report);
                Assert.IsFalse(report.HasErrors, report.ToText()); Assert.NotNull(Program);
                Program.BindLogic(new LogicBinder(environment.Facts, environment.Refs, report));
                Facts = new FactStore(environment.Facts);
                var fake = new FakeLogicContext(environment.Facts); var narrative = new NarrativeLogicAdapter();
                var context = new LogicContext(Facts, fake, narrative, fake, () => 42);
                Runner = new GraphDialogRunner(Program, context, Memory);
                narrative.BindDialog(new DialogService(Runner, DialogServiceTests.Table(), context, Memory));
            }
            public void Run(string node = "start") { Runner.Start(node, Host); Drain(Runner, Host); }
            public void Dispose() { if (Runner?.IsRunning == true) Runner.Stop(); if (Program != null) Object.DestroyImmediate(Program); environment.Dispose(); }
        }
        static string Node(string title, string body) => "title: " + title + "\n---\n" + body + "\n===\n";
        static string Start(string body) => Node("start", body);
        static void Drain(IDialogRunner runner, Host host)
        {
            int lines = 0;
            while (runner.IsRunning && host.AtLine)
            { Assert.Less(lines++, 1000, "Bounded dialogue line progression"); host.AtLine = false; runner.Continue(); }
        }
        [TearDown] public void Cleanup() { TestContentFactory.DestroyAll(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void Lines()
        {
            using var f = new Fixture(Start("First #line:one\nSecond #line:two"));
            Assert.IsTrue(f.Runner.HasNode("start")); Assert.IsFalse(f.Runner.HasNode("missing"));
            f.Runner.Start("start", f.Host); CollectionAssert.AreEqual(new[] { "First" }, f.Host.Lines);
            f.Runner.Continue(); CollectionAssert.AreEqual(new[] { "First", "Second" }, f.Host.Lines);
            f.Runner.Continue(); Assert.IsTrue(f.Host.Ended); Assert.AreEqual(1, f.Memory.GetVisitCount("start"));
        }
        [Test] public void Options_AvailabilityAndHide()
        {
            using var f = new Fixture(Start("-> Grey <<if false>> #line:grey\n    Wrong #line:wrong\n" +
                "<<if false>>\n-> Omitted #line:omitted\n<<endif>>\n" +
                "-> Hidden <<if false>> #hide #line:hidden\n-> Choose #quest #leave #line:choose\n    Selected #line:selected\nAfter #line:after"));
            f.Run(); Assert.AreEqual(3, f.Host.Options.Length); Assert.IsFalse(f.Host.Options[0].Available);
            Assert.IsFalse(f.Host.Options[1].Available); Assert.IsTrue(f.Host.Options[1].HideWhenUnavailable);
            Assert.IsTrue(f.Host.Options[2].IsQuest); Assert.IsTrue(f.Host.Options[2].IsLeave);
            f.Runner.SelectOption(f.Host.Options[0].Id); Assert.IsEmpty(f.Host.Lines);
            f.Runner.SelectOption(f.Host.Options[2].Id); Drain(f.Runner, f.Host);
            CollectionAssert.AreEqual(new[] { "Selected", "After" }, f.Host.Lines);
        }
        [Test] public void IfElse()
        {
            using var f = new Fixture(Start("<<if false>>\nWrong #line:a\n<<elseif true>>\nMiddle #line:b\n<<else>>\nWrong #line:c\n<<endif>>\n<<if false>>\nWrong #line:d\n<<else>>\nElse #line:e\n<<endif>>"));
            f.Run(); CollectionAssert.AreEqual(new[] { "Middle", "Else" }, f.Host.Lines);
        }
        [Test] public void JumpAndDetour()
        {
            using var f = new Fixture(Start("<<detour sub>>\nReturn #line:r\n<<jump end>>") + Node("sub", "Sub #line:s\n<<return>>") + Node("end", "End #line:e"));
            f.Run(); CollectionAssert.AreEqual(new[] { "Sub", "Return", "End" }, f.Host.Lines);
            CollectionAssert.AreEqual(new[] { "sub", "start", "end" }, f.Host.Completed);
        }
        [Test] public void JumpInsideDetour_KeepsReturn()
        {
            using var f = new Fixture(Start("<<detour sub>>\nReturn #line:r") + Node("sub", "<<jump end>>") + Node("end", "End #line:e"));
            f.Run(); CollectionAssert.AreEqual(new[] { "End", "Return" }, f.Host.Lines);
            CollectionAssert.AreEqual(new[] { "sub", "end", "start" }, f.Host.Completed);
        }
        [Test] public void Stop()
        {
            using var f = new Fixture(Start("<<detour sub>>\nWrong #line:w") + Node("sub", "Stop #line:s\n<<stop>>"));
            f.Run(); Assert.IsFalse(f.Runner.IsRunning); Assert.AreEqual(0, f.Memory.GetVisitCount("start")); Assert.AreEqual(1, f.Memory.GetVisitCount("sub"));
            f.Runner.Start("start", f.Host); f.Runner.Stop(); Assert.IsFalse(f.Runner.IsRunning); Assert.AreEqual(2, f.Memory.GetVisitCount("sub"));
        }
        [Test] public void SetFacts()
        {
            using var f = new Fixture(Start("<<set $intro_awake = true>>\n<<set $story_count = 3 + 4>>\n<<if $intro_awake && $story_count == 7>>\nSet #line:s\n<<endif>>"));
            f.Run(); CollectionAssert.AreEqual(new[] { "Set" }, f.Host.Lines);
            Assert.AreEqual(1, f.Facts.GetBySlot(f.Facts.Catalog.SlotOf("intro_awake"))); Assert.AreEqual(7, f.Facts.GetBySlot(f.Facts.Catalog.SlotOf("story_count")));
        }
        [Test] public void VisitedCount()
        {
            using var f = new Fixture(Start("<<detour sub>>\n<<if visited_count(\"sub\") == 1>>\nVisited #line:v\n<<endif>>") + Node("sub", "<<return>>"));
            f.Run(); CollectionAssert.AreEqual(new[] { "Visited" }, f.Host.Lines); Assert.AreEqual(1, f.Memory.GetVisitCount("sub"));
        }
        [Test] public void CommandResults()
        {
            using var f = new Fixture(Start("<<give_credits 1>>\n<<give_credits 2>>\nAfter #line:a\n<<give_credits 3>>\nWrong #line:w"));
            f.Host.Command = text => text.EndsWith("2", StringComparison.Ordinal) ? DialogCommandResult.Block : text.EndsWith("3", StringComparison.Ordinal) ? DialogCommandResult.Stop : DialogCommandResult.Continue;
            f.Run(); Assert.AreEqual(2, f.Host.Commands.Count); Assert.IsEmpty(f.Host.Lines);
            f.Runner.Continue(); Assert.IsEmpty(f.Host.Lines); f.Runner.Resume(); Drain(f.Runner, f.Host);
            CollectionAssert.AreEqual(new[] { "After" }, f.Host.Lines); Assert.IsTrue(f.Host.Ended);
        }
        [Test] public void DetourDepthLimit()
        {
            using var f = new Fixture(Start("<<detour start>>"));
            LogAssert.Expect(LogType.Error, "[Dialog] Dialog detour depth exceeds 16.");
            f.Run(); Assert.IsFalse(f.Runner.IsRunning); Assert.AreEqual(16, f.Host.Started.Count); Assert.AreEqual(1, f.Host.Completed.Count);
        }
        [Test] public void LegacyBeats_RunClean()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"); Assert.NotNull(database);
            var report = new ValidationReport("legacy runner"); var content = ContentCatalog.Build(database.Definitions, report);
            var sources = Directory.GetFiles(DialogCommandTableGenerator.Root, "*.yarn", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\','/').EndsWith("/_Generated/facts.yarn", StringComparison.Ordinal)).ToDictionary(path => path, File.ReadAllText);
            var program = DialogProgramCompiler.Compile(sources, content, report);
            Assert.IsFalse(report.HasErrors, report.ToText()); Assert.NotNull(program);
            try
            {
                var refs = RefResolverRegistry.CreateDefault(content); var facts = FactCatalog.Build(content, refs, report);
                program.BindLogic(new LogicBinder(facts, refs, report));
                int count = 0;
                foreach (var beat in content.All<StoryBeatDefinition>().Where(beat => !string.IsNullOrEmpty(beat.YarnNode)))
                {
                    var context = new FakeLogicContext(facts); var memory = new DialogMemory();
                    IDialogRunner runner = new GraphDialogRunner(program, context, memory); var host = new Host();
                    Assert.IsTrue(runner.HasNode(beat.YarnNode), beat.IdString);
                    runner.Start(beat.YarnNode, host); Drain(runner, host);
                    Assert.IsTrue(host.Ended || host.Options.Length > 0, beat.IdString); if (runner.IsRunning) runner.Stop(); count++;
                }
                Assert.Greater(count, 0); TestContext.WriteLine("Converted beat starts run: " + count);
            }
            finally { Object.DestroyImmediate(program); }
        }
    }
}
