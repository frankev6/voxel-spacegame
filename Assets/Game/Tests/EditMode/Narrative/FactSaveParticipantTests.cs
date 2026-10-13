using System;
using System.Diagnostics;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Narrative;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class FactSaveParticipantTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        static FactStore Store(int count)
        {
            var rows = new FactDeclaration[count];
            for (int i = 0; i < count; i++) rows[i] = new FactDeclaration { Name = "saved_fact_" + i.ToString("D4"), Kind = FactKind.Int };
            var store = FactStoreTests.Create(rows);
            for (int i = 0; i < count; i++) store.SetBySlot(store.Catalog.SlotOf(rows[i].Name), i + 1, FactWriteSource.Debugger);
            EventBus<FactChanged>.Drain(); return store;
        }
        static byte[] Capture(FactSaveParticipant participant)
        { using var writer = new SaveWriter(); participant.Capture(writer); return writer.ToArray(); }
        [Test] public void RoundTrip_ByteIdentical()
        {
            foreach (int count in new[] { 0, 1, 5000 })
            {
                var store = Store(count); var participant = new FactSaveParticipant(store); byte[] before = Capture(participant);
                store.ResetToDefaults(); participant.Restore(new SaveReader(before), 1); CollectionAssert.AreEqual(before, Capture(participant));
                TestContentFactory.DestroyAll();
            }
        }
        [Test] public void UnknownFact_Preserved()
        {
            var participant = new FactSaveParticipant(Store(0)); using var writer = new SaveWriter();
            writer.WriteInt32(1); writer.WriteString("unknown_saved_fact"); writer.WriteInt32(71);
            byte[] payload = writer.ToArray(); participant.Restore(new SaveReader(payload), 1); CollectionAssert.AreEqual(payload, Capture(participant));
        }
        [Test] public void Load_RaisesNoEvents()
        {
            var store = Store(1); var participant = new FactSaveParticipant(store); byte[] saved = Capture(participant);
            store.ResetToDefaults(); int events = 0; EventBus<FactChanged>.Subscribe(_ => events++);
            participant.Restore(new SaveReader(saved), 1); EventBus<FactChanged>.Drain(); Assert.AreEqual(0, events);
            Assert.AreEqual(0, store.DrainChanged(new System.Collections.Generic.List<int>()));
        }
        [Test] public void VersionZero_ResetsDefaults()
        {
            var store = Store(1); new FactSaveParticipant(store).Restore(SaveReader.Empty, 0);
            Assert.AreEqual(0, store.NonDefaultCount); Assert.AreEqual(0, store.Capture().Entries.Count);
        }
        [Test] public void Truncated_Throws()
        {
            var store = Store(1); var participant = new FactSaveParticipant(store); byte[] valid = Capture(participant);
            for (int length = 0; length < valid.Length; length++)
            {
                Assert.Throws<SaveFormatException>(() => participant.Restore(new SaveReader(valid, 0, length), 1));
                CollectionAssert.AreEqual(valid, Capture(participant), "Malformed payload must not mutate live facts.");
            }
        }
        [Test, Category("Performance")] public void Capture5k_UnderOneMs()
        {
            var participant = new FactSaveParticipant(Store(5000)); using var writer = new SaveWriter();
            participant.Capture(writer); var samples = new double[5];
            for (int i = 0; i < samples.Length; i++)
            {
                writer.Reset(); long start = Stopwatch.GetTimestamp(); participant.Capture(writer);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                Assert.Greater(writer.Length, 4);
            }
            PerfRun.Gate("Capture5kFACT", samples, 1);
        }
    }
}
