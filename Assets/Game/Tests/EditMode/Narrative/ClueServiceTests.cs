using System;
using System.Collections.Generic;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Narrative;
using Game.Narrative.Clues;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class ClueServiceTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }

        sealed class Fixture
        {
            internal readonly ClueDefinition Alpha = TestContentFactory.Create<ClueDefinition>("clue_alpha");
            internal readonly ClueDefinition Beta = TestContentFactory.Create<ClueDefinition>("clue_beta");
            internal readonly ClueDefinition Gamma = TestContentFactory.Create<ClueDefinition>("clue_gamma");
            internal readonly RumorDefinition Rumor = TestContentFactory.Create<RumorDefinition>("rumor_gate");
            internal readonly RumorDefinition Blank = TestContentFactory.Create<RumorDefinition>("rumor_blank");
            internal readonly MysteryDefinition Mystery;
            internal readonly ContentCatalog Content;
            internal readonly FactStore Facts;
            internal readonly LogicContext Context;
            internal readonly ClueService Clues;
            internal readonly NarrativeService Runtime;
            internal readonly int ResolveSlot, RewardSlot, CounterSlot;
            internal Fixture()
            {
                Mystery = TestContentFactory.Create<MysteryDefinition>("mystery_pair", so =>
                {
                    var required = so.FindProperty("requires"); required.arraySize = 2;
                    required.GetArrayElementAtIndex(0).objectReferenceValue = Alpha;
                    required.GetArrayElementAtIndex(1).objectReferenceValue = Beta;
                });
                TestContentFactory.Edit(Alpha, so => TestContentFactory.Set(so, "mystery", Mystery));
                TestContentFactory.Edit(Beta, so => TestContentFactory.Set(so, "mystery", Mystery));
                LogicTestEnvironment.Source(Mystery.OnSolved, new[] { "add_fact story_rewards 1" });
                LogicTestEnvironment.Source(Rumor.ResolvedWhen, "$story_resolve");
                var definitions = FactCatalogTests.BaseContent();
                definitions.AddRange(new ContentDefinition[] { Alpha, Beta, Gamma, Rumor, Blank, Mystery,
                    FactCatalogTests.Facts("facts_clues", new FactDeclaration { Name = "story_rewards", Kind = FactKind.Int },
                        new FactDeclaration { Name = "story_resolve", Kind = FactKind.Bool }) });
                Content = FactCatalogTests.Content(definitions); var refs = RefResolverRegistry.CreateDefault(Content);
                var report = new ValidationReport("Clue service fixture"); var factCatalog = FactCatalog.Build(Content, refs, report);
                Assert.IsFalse(report.HasErrors, report.ToText());
                var compiler = new LogicCompiler(factCatalog, refs, new LogicFunctionRegistry());
                var binder = new LogicBinder(factCatalog, refs, report);
                Mystery.CompileLogic(compiler, report.For("test:mystery")); Mystery.BindLogic(binder);
                Rumor.CompileLogic(compiler, report.For("test:rumor")); Rumor.BindLogic(binder);
                Blank.CompileLogic(compiler, report.For("test:blank")); Blank.BindLogic(binder);
                Assert.IsFalse(report.HasErrors, report.ToText());
                Facts = new FactStore(factCatalog);
                // Unused economy/narrative/world facades; all evaluated facts, scopes and effects use the real owners.
                var owners = new FakeLogicContext(factCatalog) { UniversalTime = 23 };
                Context = new LogicContext(Facts, owners, owners, owners, () => 7);
                Facts.BindTraceWorld(owners);
                Clues = new ClueService(Content, Facts, Context);
                Runtime = new NarrativeService(Facts, Context); Runtime.Register(Clues);
                Runtime.BeginNewGame(); Runtime.SetWorldReady(true); Runtime.Tick();
                ResolveSlot = factCatalog.SlotOf("story_resolve"); RewardSlot = factCatalog.SlotOf("story_rewards"); CounterSlot = factCatalog.SlotOf("sys_clue_counter");
            }
            internal int Index(ClueDefinition clue) => Content.IndexOf<ClueDefinition>(clue);
            internal int MysteryIndex => Content.IndexOf(Mystery);
        }

        [Test] public void Ordinals()
        {
            var f = new Fixture(); var changes = new List<int>(); f.Clues.ClueChanged += changes.Add;
            int beta = f.Index(f.Beta), alpha = f.Index(f.Alpha), gamma = f.Index(f.Gamma);
            Assert.IsTrue(f.Clues.Reveal(beta)); Assert.IsTrue(f.Clues.Reveal(alpha)); Assert.IsTrue(f.Clues.Reveal(gamma));
            Assert.AreEqual(1, f.Clues.OrdinalOf(beta)); Assert.AreEqual(2, f.Clues.OrdinalOf(alpha)); Assert.AreEqual(3, f.Clues.OrdinalOf(gamma));
            Assert.AreEqual(3, f.Facts.GetBySlot(f.CounterSlot)); CollectionAssert.AreEqual(new[] { beta, alpha, gamma }, changes);
            Assert.IsTrue(f.Clues.IsKnown(alpha)); Assert.AreEqual(0, f.Clues.OrdinalOf(0)); Assert.IsFalse(f.Clues.IsKnown(int.MaxValue));
            Assert.IsTrue(f.Clues.ConsumeMarkersDirty()); Assert.IsFalse(f.Clues.ConsumeMarkersDirty());
            var trace = new LogicTraceEntry[64]; int count = f.Facts.Trace.CopyTo(trace), reveals = 0;
            for (int i = 0; i < count; i++) if (trace[i].Kind == LogicTraceKind.ClueRevealed)
            { reveals++; Assert.AreEqual(FactWriteSource.Clue, trace[i].Source); Assert.AreEqual(23, trace[i].UniversalTime); }
            Assert.AreEqual(3, reveals);
        }

        [Test] public void Reveal_Idempotent()
        {
            var f = new Fixture(); int clue = f.Index(f.Gamma), changes = 0;
            f.Clues.ClueChanged += _ => changes++;
            f.Clues.MarkSeen(clue); Assert.IsFalse(f.Clues.IsSeen(clue));
            Assert.IsTrue(f.Clues.Reveal(clue)); var saved = f.Facts.Capture(); int traceBefore = f.Facts.Trace.Version;
            Assert.IsFalse(f.Clues.Reveal(clue)); Assert.AreEqual(1, changes); Assert.AreEqual(traceBefore, f.Facts.Trace.Version);
            var after = f.Facts.Capture(); Assert.AreEqual(saved.Entries.Count, after.Entries.Count);
            for (int i = 0; i < saved.Entries.Count; i++) { Assert.AreEqual(saved.Entries[i].Name, after.Entries[i].Name); Assert.AreEqual(saved.Entries[i].Value, after.Entries[i].Value); }
            f.Clues.MarkSeen(clue); f.Clues.MarkSeen(clue); Assert.IsTrue(f.Clues.IsSeen(clue)); Assert.AreEqual(2, changes);
            Assert.IsFalse(f.Clues.Reveal(-1)); Assert.IsFalse(f.Clues.Reveal(0));
            f.Runtime.BeginNewGame(); Assert.IsFalse(f.Clues.IsSeen(clue)); Assert.IsFalse(f.Clues.IsKnown(clue)); Assert.AreEqual(0, f.Facts.GetBySlot(f.CounterSlot));
        }

        [Test] public void Mystery_SolvesOnce()
        {
            var f = new Fixture(); int solved = 0;
            f.Clues.MysterySolved += index =>
            {
                Assert.AreEqual(f.MysteryIndex, index); Assert.IsTrue(f.Clues.IsSolved(index));
                Assert.AreEqual(1, f.Facts.GetBySlot(f.RewardSlot)); solved++;
                Assert.IsFalse(f.Clues.Reveal(f.Index(f.Alpha)));
            };
            var callerScope = new LogicScope(LogicUsage.BeatEffect, 17, 2, 9, false, default); f.Context.PushScope(callerScope);
            Assert.IsTrue(f.Clues.Reveal(f.Index(f.Alpha))); Assert.IsFalse(f.Clues.IsSolved(f.MysteryIndex));
            Assert.AreEqual(0, f.Facts.GetBySlot(f.RewardSlot));
            Assert.IsTrue(f.Clues.Reveal(f.Index(f.Beta))); Assert.AreEqual(1, solved);
            Assert.AreEqual(callerScope.QuestIndex, f.Context.Scope.QuestIndex); Assert.AreEqual(callerScope.BeatIndex, f.Context.Scope.BeatIndex); f.Context.PopScope();
            f.Clues.Reveal(f.Index(f.Gamma)); f.Clues.Reveal(f.Index(f.Beta)); f.Runtime.Tick();
            Assert.AreEqual(1, f.Facts.GetBySlot(f.RewardSlot)); Assert.AreEqual(1, solved);
            Assert.IsFalse(f.Clues.IsSolved(0)); Assert.IsFalse(f.Clues.IsResolved(f.Index(f.Alpha)));
        }

        [Test] public void Rumor_ResolvesAndLatches()
        {
            var f = new Fixture(); int rumor = f.Index(f.Rumor), blank = f.Index(f.Blank), changes = 0;
            f.Clues.ClueChanged += _ => changes++;
            f.Clues.Reveal(rumor); f.Clues.Reveal(blank); f.Runtime.Tick(); Assert.AreEqual(2, changes);
            Assert.IsFalse(f.Clues.IsResolved(rumor)); Assert.IsFalse(f.Clues.IsResolved(blank));
            f.Clues.ConsumeMarkersDirty(); f.Facts.SetBySlot(f.ResolveSlot, 1, FactWriteSource.Debugger);
            f.Clues.Evaluate(new[] { new LogicDependency(LogicDependencyKind.Fact, f.RewardSlot) }, false);
            Assert.IsFalse(f.Clues.IsResolved(rumor), "An unrelated change must not poll the now-true condition.");
            f.Clues.Evaluate(new[] { new LogicDependency(LogicDependencyKind.Fact, f.ResolveSlot) }, false);
            Assert.IsTrue(f.Clues.IsResolved(rumor)); Assert.AreEqual(3, changes); Assert.IsTrue(f.Clues.ConsumeMarkersDirty());
            f.Facts.SetBySlot(f.ResolveSlot, 0, FactWriteSource.Debugger); f.Runtime.Tick();
            f.Clues.Evaluate(Array.Empty<LogicDependency>(), true);
            Assert.IsTrue(f.Clues.IsResolved(rumor)); Assert.IsFalse(f.Clues.IsResolved(blank)); Assert.AreEqual(3, changes);
            Assert.AreEqual(0, f.Facts.GetBySlot(f.RewardSlot));
            var alreadyTrue = new Fixture(); alreadyTrue.Facts.SetBySlot(alreadyTrue.ResolveSlot, 1, FactWriteSource.Debugger);
            alreadyTrue.Runtime.Tick(); Assert.IsFalse(alreadyTrue.Clues.IsResolved(alreadyTrue.Index(alreadyTrue.Rumor)));
            alreadyTrue.Clues.Reveal(alreadyTrue.Index(alreadyTrue.Rumor)); alreadyTrue.Runtime.Tick();
            Assert.IsTrue(alreadyTrue.Clues.IsResolved(alreadyTrue.Index(alreadyTrue.Rumor)), "An already-true rumor latches on its first evaluation after reveal.");
        }

        [Test] public void Restore_Silent()
        {
            var original = new Fixture(); original.Clues.Reveal(original.Index(original.Alpha)); original.Clues.Reveal(original.Index(original.Beta));
            original.Clues.Reveal(original.Index(original.Rumor)); original.Clues.MarkSeen(original.Index(original.Alpha));
            original.Facts.SetBySlot(original.ResolveSlot, 1, FactWriteSource.Debugger); original.Runtime.Tick();
            var savedFacts = original.Facts.Capture(); var seen = new List<ContentId>(); var resolved = new List<ContentId>();
            original.Clues.CaptureState(seen, resolved); Assert.IsTrue(resolved.Remove(original.Rumor.Id));
            ContentId orphanClue = new ContentId("clue_removed"), orphanRumor = new ContentId("rumor_removed"); seen.Add(orphanClue); resolved.Add(orphanRumor);
            var f = new Fixture(); int changed = 0, solved = 0;
            f.Clues.ClueChanged += _ => changed++; f.Clues.MysterySolved += _ => solved++;
            f.Runtime.SetWorldReady(false); f.Facts.Restore(savedFacts); f.Clues.RestoreState(seen, resolved); f.Runtime.OnSessionRestored();
            f.Runtime.Tick(); Assert.IsFalse(f.Clues.IsResolved(f.Index(f.Rumor))); Assert.AreEqual(0, changed);
            f.Runtime.SetWorldReady(true); f.Runtime.Tick(); f.Runtime.Tick();
            Assert.IsTrue(f.Clues.IsResolved(f.Index(f.Rumor))); Assert.IsTrue(f.Clues.IsSeen(f.Index(f.Alpha)));
            Assert.IsTrue(f.Clues.IsSolved(f.MysteryIndex)); Assert.AreEqual(1, f.Facts.GetBySlot(f.RewardSlot));
            Assert.AreEqual(0, changed); Assert.AreEqual(0, solved);
            var afterSeen = new List<ContentId>(); var afterResolved = new List<ContentId>(); f.Clues.CaptureState(afterSeen, afterResolved);
            CollectionAssert.Contains(afterSeen, orphanClue); CollectionAssert.Contains(afterResolved, orphanRumor);
            CollectionAssert.Contains(afterResolved, f.Rumor.Id);
            for (int i = 1; i < afterSeen.Count; i++) Assert.Less(string.CompareOrdinal(afterSeen[i - 1].Value, afterSeen[i].Value), 0);
            f.Clues.RestoreState(Array.Empty<ContentId>(), Array.Empty<ContentId>()); Assert.IsFalse(f.Clues.IsSeen(f.Index(f.Alpha)));
            Assert.IsFalse(f.Clues.IsResolved(f.Index(f.Rumor)));
            // Generated mystery status is authoritative; a load does not derive completion or execute rewards.
            savedFacts.Entries.RemoveAll(entry => entry.Name == f.Mystery.IdString); f.Facts.Restore(savedFacts);
            f.Runtime.OnSessionRestored(); f.Runtime.Tick();
            Assert.IsFalse(f.Clues.IsSolved(f.MysteryIndex)); Assert.AreEqual(1, f.Facts.GetBySlot(f.RewardSlot));
            Assert.AreEqual(0, changed); Assert.AreEqual(0, solved);
            original.Facts.SetBySlot(original.ResolveSlot, 0, FactWriteSource.Debugger); original.Runtime.Tick();
            original.Clues.CaptureState(seen, resolved); f.Clues.RestoreState(seen, resolved); f.Clues.OnRestored();
            f.Facts.SetBySlot(f.ResolveSlot, 0, FactWriteSource.Debugger);
            f.Clues.Evaluate(Array.Empty<LogicDependency>(), true); Assert.IsTrue(f.Clues.IsResolved(f.Index(f.Rumor)), "A saved latch survives even when its condition becomes false.");
        }
    }
}
