using System.Collections.Generic;
using System.Linq;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Validation;
using Game.Narrative;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class FactStoreTests
    {
        FactStore store;
        int number, flag, session;
        [SetUp] public void SetUp()
        {
            EventHub.Reset();
            store = Create(
                new FactDeclaration { Name = "story_number", Kind = FactKind.Int, Default = 3, Min = -10, Max = 20, FormerNames = new[] { "story_old" } },
                new FactDeclaration { Name = "story_flag", Kind = FactKind.Bool },
                new FactDeclaration { Name = "story_session", Kind = FactKind.Int, Scope = FactScope.Session });
            number = store.Catalog.SlotOf("story_number"); flag = store.Catalog.SlotOf("story_flag"); session = store.Catalog.SlotOf("story_session");
        }
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        internal static FactStore Create(params FactDeclaration[] rows)
        {
            var definitions = FactCatalogTests.BaseContent(); definitions.Add(FactCatalogTests.Facts("facts_store", rows));
            var content = FactCatalogTests.Content(definitions); var report = new ValidationReport("store");
            var catalog = FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText()); return new FactStore(catalog, 1);
        }
        [Test] public void Set_SameValue_NoEvent()
        {
            int count = 0; EventBus<FactChanged>.Subscribe(_ => count++);
            store.SetBySlot(number, 8, FactWriteSource.Effect); store.SetBySlot(number, 8, FactWriteSource.Effect);
            EventBus<FactChanged>.Drain(); Assert.AreEqual(1, count); Assert.AreEqual(1, store.NonDefaultCount);
            var changed = new List<int>(); Assert.AreEqual(1, store.DrainChanged(changed)); CollectionAssert.AreEqual(new[] { number }, changed);
        }
        [Test] public void Event_IsDeferred()
        {
            FactChanged received = default; int count = 0; EventBus<FactChanged>.Subscribe(e => { received = e; count++; });
            store.SetBySlot(number, 4, FactWriteSource.Quest); Assert.AreEqual(0, count);
            EventBus<FactChanged>.Drain(); Assert.AreEqual(1, count); Assert.AreEqual(3, received.OldValue);
            Assert.AreEqual(4, received.NewValue); Assert.AreEqual(FactWriteSource.Quest, received.Source);
        }
        [Test] public void Restore_RaisesNothing()
        {
            int count = 0; EventBus<FactChanged>.Subscribe(_ => count++);
            var save = new FactSnapshot(); save.Entries.Add(new FactEntry("story_number", 10));
            int version = store.Trace.Version; store.Restore(save); EventBus<FactChanged>.Drain();
            Assert.AreEqual(0, count); Assert.AreEqual(version, store.Trace.Version); Assert.AreEqual(10, store.GetBySlot(number));
            Assert.AreEqual(0, store.DrainChanged(new List<int>()));
        }
        [Test] public void RoundTrip_PreservesOrphans()
        {
            var save = new FactSnapshot(); save.Entries.Add(new FactEntry("unknown_before", 77));
            save.Entries.Add(new FactEntry("story_number", 15)); save.Entries.Add(new FactEntry("aaa_missing", -2));
            store.Restore(save); var first = store.Capture(); store.Restore(first); var second = store.Capture();
            CollectionAssert.AreEqual(first.Entries.Select(x => (x.Name, x.Value)), second.Entries.Select(x => (x.Name, x.Value)));
            CollectionAssert.AreEqual(new[] { "aaa_missing", "story_number", "unknown_before" }, second.Entries.Select(x => x.Name));
            store.ResetToDefaults(); Assert.AreEqual(0, store.Capture().Entries.Count); Assert.AreEqual(0, store.NonDefaultCount);
        }
        [Test] public void FormerName_LoadsIntoLiveSlot()
        {
            var save = new FactSnapshot(); save.Entries.Add(new FactEntry("story_old", 9)); store.Restore(save);
            Assert.AreEqual(9, store.GetBySlot(number)); Assert.AreEqual("story_number", store.Capture().Entries[0].Name);
            save.Entries.Add(new FactEntry("story_number", 11)); store.Restore(save); Assert.AreEqual(11, store.GetBySlot(number));
            save.Entries.Reverse(); store.Restore(save); Assert.AreEqual(11, store.GetBySlot(number));
        }
        [Test] public void SessionScope_NotSaved()
        {
            store.SetBySlot(session, 12, FactWriteSource.WorldEvent); Assert.AreEqual(0, store.Capture().Entries.Count);
            var save = new FactSnapshot(); save.Entries.Add(new FactEntry("story_session", 18)); store.Restore(save);
            Assert.AreEqual(0, store.GetBySlot(session)); Assert.AreEqual(0, store.DrainChanged(new List<int>()));
        }
        [Test] public void BoolCoercion_AndClamp()
        {
            store.SetBySlot(flag, -2, FactWriteSource.Effect); Assert.AreEqual(1, store.GetBySlot(flag));
            store.SetBySlot(number, int.MaxValue, FactWriteSource.Effect); Assert.AreEqual(20, store.GetBySlot(number));
            store.AddBySlot(number, int.MinValue, FactWriteSource.Effect); Assert.AreEqual(-10, store.GetBySlot(number));
            var changed = new List<int>(); Assert.AreEqual(2, store.DrainChanged(changed)); CollectionAssert.AreEqual(new[] { flag, number }, changed);
            store.SetBySlot(flag, 0, FactWriteSource.Effect); Assert.AreEqual(0, store.GetBySlot(flag));
        }
    }
}
