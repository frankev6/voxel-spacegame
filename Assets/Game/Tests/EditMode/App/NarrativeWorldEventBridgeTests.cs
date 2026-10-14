using System;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Validation;
using Game.Narrative;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Game.Tests.EditMode.Data.Logic.LogicTestEnvironment;

namespace Game.Tests.EditMode.App
{
    public sealed class NarrativeWorldEventBridgeTests
    {
        NarrativeWorldEventBridge bridge;
        FactStore facts;
        ContentHash body, item;
        int selectedCount, countAtSelection, unhandled;
        void Setup()
        {
            EventHub.Reset(); GameLog.ResetOnceKeys();
            var defs = FactCatalogTests.BaseContent();
            var template = TestContentFactory.Create<BodyTemplateDefinition>("body_counter"); defs.Add(template); body = template.Id.Hash;
            item = new ContentId("item_missing").Hash;
            var content = FactCatalogTests.Content(defs); var report = new ValidationReport("world events");
            var catalog = FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            facts = new FactStore(catalog); var unused = new FakeLogicContext(catalog);
            var context = new LogicContext(facts, unused, unused, unused, () => 7);
            var host = new NarrativeService(facts, context); host.BeginNewGame(); host.SetPlayerCanTalk(true);
            bridge = new NarrativeWorldEventBridge(host,
                (s, key) => new LogicBodyHandle(s, (byte)BodyRole.Station, 0, 0, body),
                s => default, member => null, () => true, hash => 0,
                (handle, canTalk) => { selectedCount++; countAtSelection = Value(FactFamily.Interacted, body); Assert.IsTrue(canTalk); return false; },
                (hash, member, talk) => { }, (system, first) => { });
            EventBus<InteractionUnhandled>.Subscribe(value => unhandled++);
        }
        int Value(FactFamily family, ContentHash hash) => facts.GetBySlot(facts.Catalog.GeneratedSlot(family, hash.Value));
        void Interact()
        {
            EventBus<BodyInteracted>.Publish(new BodyInteracted(1, 1, (byte)InteractionKind.Inspect, 0));
            EventBus<BodyInteracted>.Drain(); EventBus<InteractionUnhandled>.Drain();
        }
        [TearDown] public void Cleanup()
        { bridge?.Dispose(); EventHub.Reset(); GameLog.ResetOnceKeys(); TestContentFactory.DestroyAll(); }
        [Test] public void Interaction_CountsThenSelects()
        {
            Setup(); Interact();
            Assert.AreEqual(1, countAtSelection); Assert.AreEqual(1, selectedCount); Assert.AreEqual(1, unhandled);
            EventBus<BodyInteracted>.Publish(new BodyInteracted(1, 1, (byte)InteractionKind.Salvage, 0));
            EventBus<BodyInteracted>.Publish(new BodyInteracted(1, 0, (byte)InteractionKind.Inspect, 0));
            EventBus<BodyInteracted>.Drain(); Assert.AreEqual(1, selectedCount);
        }
        [Test] public void PlayerShipDestroyed_Ignored()
        {
            Setup();
            EventBus<ShipDestroyed>.Publish(new ShipDestroyed(1, body, default, 0, 1, 1, 0, 0, 0, 0));
            EventBus<ShipDestroyed>.Drain(); Assert.AreEqual(0, Value(FactFamily.Destroyed, body));
            EventBus<ShipDestroyed>.Publish(new ShipDestroyed(1, body, default, 0, 1, 0, 0, 0, 0, 0));
            EventBus<ShipDestroyed>.Drain(); Assert.AreEqual(1, Value(FactFamily.Destroyed, body));
        }
        [Test, Category("Performance")] public void Events_DoNotAllocate()
        {
            Setup();
            var acquired = new ItemAcquired(item, 1, 1, AcquireSource.Loot);
            for (int i = 0; i < 32; i++) { EventBus<ItemAcquired>.Publish(acquired); EventBus<ItemAcquired>.Drain(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { EventBus<ItemAcquired>.Publish(acquired); EventBus<ItemAcquired>.Drain(); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(1032, Value(FactFamily.Acquired, item));
            Assert.AreEqual(0, bytes, "1000 warmed world events must not allocate");
        }
        [Test] public void Dispose_Unsubscribes()
        {
            Setup(); bridge.Dispose(); bridge.Dispose(); Interact();
            Assert.AreEqual(0, Value(FactFamily.Interacted, body)); Assert.AreEqual(0, selectedCount);
            Assert.AreEqual(0, EventBus<ItemAcquired>.SubscriberCount);
            Assert.AreEqual(0, EventBus<ShipDestroyed>.SubscriberCount);
        }
        [Test] public void UnknownId_WarnsOnce()
        {
            Setup(); var hash = new ContentHash(987654321);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("World event references unknown narrative ID 987654321"));
            for (int i = 0; i < 2; i++) { EventBus<ItemAcquired>.Publish(new ItemAcquired(hash, 0, 1, AcquireSource.Dev)); EventBus<ItemAcquired>.Drain(); }
            Assert.AreEqual(1, GameLog.GetOnceCount("NAR.WorldEvent.Unknown.987654321"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
