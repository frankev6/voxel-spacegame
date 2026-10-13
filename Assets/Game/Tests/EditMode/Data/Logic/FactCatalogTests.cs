using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data.Logic
{
    [Category("Narrative")]
    public sealed class FactCatalogTests
    {
        [TearDown] public void Cleanup() => DestroyAll();

        internal static FactCatalogDefinition Facts(string id, params FactDeclaration[] facts)
        {
            var asset = Create<FactCatalogDefinition>(id);
            typeof(FactCatalogDefinition).GetField("facts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, facts);
            return asset;
        }
        internal static List<ContentDefinition> BaseContent() => new List<ContentDefinition> {
            Block("block_missing", BlockKind.Structural), Item("item_missing")
        };
        internal static ContentCatalog Content(List<ContentDefinition> definitions)
        {
            var report = new ValidationReport("catalog fixture");
            var content = ContentCatalog.Build(definitions, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            return content;
        }

        [Test] public void Slots_AreOrdinalAndStable()
        {
            var definitions = BaseContent();
            definitions.Add(Facts("facts_zeta", new FactDeclaration { Name = "story_zeta", Kind = FactKind.Int, Default = 3 }));
            definitions.Add(Facts("facts_alpha", new FactDeclaration { Name = "story_alpha", Kind = FactKind.Bool, Default = 1 }));
            var content = Content(definitions); var report = new ValidationReport("facts");
            var a = FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), report);
            definitions.Reverse(); content = Content(definitions);
            var b = FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), report);
            var names = Enumerable.Range(0, a.Count).Select(a.IdAt).Select(x => x.Name).ToArray();
            CollectionAssert.AreEqual(names.OrderBy(x => x, System.StringComparer.Ordinal), names);
            CollectionAssert.AreEqual(names, Enumerable.Range(0, b.Count).Select(b.IdAt).Select(x => x.Name));
            int slot = a.SlotOf("story_zeta");
            Assert.AreEqual(slot, a.SlotOf(new FactId("story_zeta")));
            Assert.AreEqual(3, a.DefaultAt(slot)); Assert.IsTrue(a.IsWritableByContent(slot));
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }

        [Test] public void GeneratedFamilies_Present()
        {
            var definitions = BaseContent(); definitions.Add(Item("resource_fuel"));
            definitions.Add(Create<StoryBeatDefinition>("beat_example"));
            var content = Content(definitions); var report = new ValidationReport("facts");
            var refs = RefResolverRegistry.CreateDefault(content);
            var facts = FactCatalog.Build(content, refs, report);
            int beat = facts.SlotOf("beat_example"), acquired = facts.SlotOf("acquired_resource_fuel");
            Assert.GreaterOrEqual(beat, 0); Assert.GreaterOrEqual(acquired, 0);
            Assert.AreEqual(FactFamily.Beat, facts.FamilyAt(beat));
            Assert.IsFalse(facts.IsWritableByContent(beat));
            Assert.AreEqual(FactKind.Int, facts.KindAt(beat)); Assert.AreEqual(FactScope.Save, facts.ScopeAt(beat));
            ulong hash = new ContentId("resource_fuel").Hash.Value;
            Assert.AreEqual(acquired, facts.GeneratedSlot(FactFamily.Acquired, hash));
            Assert.AreEqual(hash, facts.SourceHashAt(acquired));
            Assert.AreEqual(-1, facts.GeneratedSlot(FactFamily.Acquired, 0));
            Assert.IsTrue(refs.TryGet(RefKind.Item, out var resolver));
            Assert.IsTrue(resolver.TryResolve("resource_fuel", out int index));
            Assert.AreEqual(content.IndexOf(content.Get<ItemDefinition>(new ContentId("resource_fuel"))), index);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }

        [Test] public void FormerNames_Resolve()
        {
            var definitions = BaseContent(); definitions.Add(Facts("facts_alias",
                new FactDeclaration { Name = "story_current", FormerNames = new[] { "story_old" } }));
            var content = Content(definitions); var report = new ValidationReport("facts");
            var facts = FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), report);
            Assert.AreEqual(facts.SlotOf("story_current"), facts.SlotOf("story_old"));
            Assert.GreaterOrEqual(facts.SlotOf("story_old"), 0);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }

        [Test] public void Validation_Codes()
        {
            var definitions = BaseContent(); definitions.Add(Facts("facts_errors",
                new FactDeclaration { Name = "BadName" },
                new FactDeclaration { Name = "sys_forbidden" },
                new FactDeclaration { Name = "story_duplicate" },
                new FactDeclaration { Name = "story_duplicate" },
                new FactDeclaration { Name = "story_bool", Kind = FactKind.Bool, Default = 2 },
                new FactDeclaration { Name = "story_code", Flags = FactFlags.ReadByCode },
                new FactDeclaration { Name = "story_journal", Flags = FactFlags.Journal }));
            var content = Content(definitions); var report = new ValidationReport("facts");
            FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), report, _ => 7UL);
            var codes = report.Entries.Select(x => x.Code).ToArray();
            foreach (string code in new[] { "NAR-E014", "NAR-E015", "NAR-E018", "NAR-E020", "NAR-E021", "NAR-E022" })
                CollectionAssert.Contains(codes, code);
        }
    }
}
