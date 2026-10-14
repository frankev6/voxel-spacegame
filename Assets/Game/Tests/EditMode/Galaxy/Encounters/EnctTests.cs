using System;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    /// <summary>ENC-008's three required cases over the same state owned by the real director.</summary>
    public sealed class EnctTests : GameTestBase
    {
        [TearDown] public void ContentCleanup() => TestContentFactory.DestroyAll();
        static byte[] Capture(EncounterSaveParticipant save)
        { using var writer = new SaveWriter(); save.Capture(writer); return writer.ToArray(); }
        static ContentCatalog Catalog(out EncounterDefinition known)
        {
            known = TestContentFactory.Create<EncounterDefinition>("encounter_alpha");
            return ContentCatalog.Build(new ContentDefinition[] { known }, new ValidationReport("ENCT"));
        }
        static void Entry(SaveWriter writer, string id, uint instance, bool combat = false)
        {
            var system = new StarSystemId(0x80000001);
            var body = new BodyId(system, BodyRole.Station, 3, 4);
            writer.WriteContentId(new ContentId(id)); writer.WriteUInt32(instance); writer.WriteUInt32(system.Value);
            writer.WriteByte((byte)EncounterAnchorKind.Body); writer.WriteUInt64(body.LocalKey); writer.WriteString("retained tag"); writer.WriteBool(combat);
        }
        static byte[] Payload(string unknown = "encounter_missing", bool duplicate = false)
        {
            using var writer = new SaveWriter(); writer.WriteUInt32(42); writer.WriteVarUInt32(2);
            writer.WriteContentId(new ContentId("encounter_alpha")); writer.WriteContentId(new ContentId(unknown)); writer.WriteVarUInt32(2);
            Entry(writer, "encounter_alpha", 4); Entry(writer, unknown, duplicate ? 4u : 7u, true); return writer.ToArray();
        }
        [Test] public void RoundTrip_ByteIdentical()
        {
            var catalog = Catalog(out _); var state = new EncounterSaveState(); var save = new EncounterSaveParticipant(state, catalog);
            byte[] payload = Payload(); save.Restore(new SaveReader(payload), 1); CollectionAssert.AreEqual(payload, Capture(save));
            Assert.AreEqual(42, state.NextInstanceSerial); Assert.AreEqual(2, state.Pending.Count);
            // Save/migration edge cases: malformed payloads cannot partially replace valid live state.
            foreach (var malformed in new[] { payload.AsSpan(0, payload.Length - 1).ToArray(), Payload(duplicate: true), Append(payload) })
            {
                Assert.Throws<SaveFormatException>(() => save.Restore(new SaveReader(malformed), 1));
                CollectionAssert.AreEqual(payload, Capture(save));
            }
            Assert.Throws<SaveFormatException>(() => save.Restore(new SaveReader(payload), 2));
            CollectionAssert.AreEqual(payload, Capture(save));
            save.Restore(SaveReader.Empty, 0); Assert.AreEqual(1, state.NextInstanceSerial); Assert.IsEmpty(state.Pending); Assert.IsEmpty(state.GloballyUsed);
        }
        static byte[] Append(byte[] data) { var copy = new byte[data.Length + 1]; Array.Copy(data, copy, data.Length); return copy; }
        [Test] public void UnknownEncounter_Preserved()
        {
            var catalog = Catalog(out _); var state = new EncounterSaveState(); var save = new EncounterSaveParticipant(state, catalog);
            byte[] payload = Payload("encounter_missing_raw_payload"); int warnings = 0;
            UnityEngine.Application.LogCallback handler = (message, trace, type) => { if (type == UnityEngine.LogType.Warning && message.Contains("encounter_missing_raw_payload")) warnings++; };
            UnityEngine.Application.logMessageReceived += handler;
            try
            {
                save.Restore(new SaveReader(payload), 1); save.Restore(new SaveReader(payload), 1);
                Assert.AreEqual(1, warnings); CollectionAssert.AreEqual(payload, Capture(save));
                var unknown = state.Pending.Find(item => item.InstanceId == 7);
                Assert.AreEqual("encounter_missing_raw_payload", unknown.Encounter.Value); Assert.IsTrue(unknown.Combat);
                Assert.AreEqual("retained tag", unknown.Anchor.Tag); Assert.IsFalse(catalog.TryGet<EncounterDefinition>(unknown.Encounter, out _));
                state.Reset(SessionResetReason.Loaded); CollectionAssert.AreEqual(payload, Capture(save), "Post-restore Loaded event preserves ENCT.");
            }
            finally { UnityEngine.Application.logMessageReceived -= handler; }
        }
        [Test] public void Restore_ActiveCleared()
        {
            var catalog = Catalog(out var known); var state = new EncounterSaveState { NextInstanceSerial = 6 };
            var active = new EncounterInstance { Id = 5, Definition = known, Persistent = true, State = InstanceState.Active,
                StarSystem = new StarSystemId(0x80000001), Source = EncounterSource.Combat, Anchor = EncounterAnchor.Player(), AliveMembers = 1 };
            active.Members.Add(123); state.Active.Add(active); state.Members.Add(123, 5);
            var save = new EncounterSaveParticipant(state, catalog); byte[] bytes = Capture(save);
            Assert.AreEqual(1, state.Active.Count, "Capture is non-mutating."); Assert.IsEmpty(state.Pending);
            save.Restore(new SaveReader(bytes), 1);
            Assert.IsEmpty(state.Active); Assert.IsEmpty(state.Members); Assert.AreEqual(1, state.Pending.Count);
            Assert.AreEqual(5, state.Pending[0].InstanceId); Assert.IsTrue(state.Pending[0].Combat);
            CollectionAssert.AreEqual(bytes, Capture(save));
            state.Reset(SessionResetReason.Loaded); Assert.AreEqual(1, state.Pending.Count);
            state.Reset(SessionResetReason.NewGame); Assert.IsEmpty(state.Pending); Assert.AreEqual(1, state.NextInstanceSerial);
        }
    }
}
