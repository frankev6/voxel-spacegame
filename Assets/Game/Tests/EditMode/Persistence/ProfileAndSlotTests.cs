using System;
using System.IO;
using System.Linq;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-002: profiles and slots (SPEC-05 §5.2) on a temporary save root.</summary>
    public sealed class ProfileAndSlotTests
    {
        string root;
        SavePaths paths;
        readonly NewtonsoftSaveJsonCodec json = new NewtonsoftSaveJsonCodec();

        [SetUp] public void SetUp() { root = Path.Combine(Path.GetTempPath(), "ProfileAndSlotTests_" + Guid.NewGuid().ToString("N")); paths = new SavePaths(root); }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        SaveService Service(SaveServiceConfig config = null) => new SaveService(paths, null, null, config ?? new SaveServiceConfig());

        SlotRef Slot(string profile, string slot) => new SlotRef(new ProfileId(profile), new SlotId(slot));

        void WriteMeta(SlotRef s, DateTime savedUtc)
        {
            Directory.CreateDirectory(paths.SlotDir(s));
            var meta = new SaveMeta { FormatVersion = 1, ProfileId = s.Profile.Value, SlotId = s.Slot.Value, DisplayName = "x", Reason = SaveReason.Manual, SavedUtc = savedUtc };
            File.WriteAllBytes(paths.MetaFile(s), json.Serialize(meta));
            File.WriteAllBytes(paths.WorldFile(s), new byte[] { 1, 2, 3 });
        }

        [Test]
        public void CreateProfile_LowestFreeId()
        {
            var svc = Service();
            var a = svc.CreateProfile("  Alice  ");
            Assert.AreEqual("p001", a.Id.Value); Assert.AreEqual("Alice", a.DisplayName); Assert.AreEqual("p001", svc.ActiveProfile.Value);
            var b = svc.CreateProfile("   ");
            Assert.AreEqual("p002", b.Id.Value); Assert.AreEqual("Commander", b.DisplayName); Assert.AreEqual("p002", svc.ActiveProfile.Value);
            var c = svc.CreateProfile(new string('z', 40));
            Assert.AreEqual(32, c.DisplayName.Length);
            Assert.IsTrue(svc.DeleteProfile(a.Id));
            Assert.IsFalse(svc.DeleteProfile(a.Id), "already gone");
            Assert.AreEqual("p001", svc.CreateProfile("Again").Id.Value, "lowest free number is reused");

            var listed = Service().ListProfiles();
            CollectionAssert.AreEquivalent(new[] { "p001", "p002", "p003" }, listed.Select(p => p.Id.Value));
            Assert.AreEqual("Commander", listed.Single(p => p.Id.Value == "p002").DisplayName);
            Assert.IsTrue(listed.All(p => p.LastSlotId == null));
            Assert.IsTrue(listed.Zip(listed.Skip(1), (x, y) => x.CreatedUtc <= y.CreatedUtc).All(ok => ok), "sorted by CreatedUtc");

            File.WriteAllText(paths.ProfileFile(new ProfileId("p002")), "{ not json");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Skipping unreadable profile"));
            Assert.AreEqual(2, Service().ListProfiles().Count, "unreadable profile skipped");
        }

        [Test]
        public void ListSlots_StatusRules()
        {
            var svc = Service();
            var t = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
            WriteMeta(Slot("p001", "manual_001"), t);
            Directory.CreateDirectory(paths.SlotDir(Slot("p001", "manual_002")));
            File.WriteAllBytes(paths.WorldFile(Slot("p001", "manual_002")), new byte[] { 9 });                      // world.sav without meta.json
            Directory.CreateDirectory(paths.SlotDir(Slot("p001", "manual_003")));
            File.WriteAllBytes(paths.BackupFile(Slot("p001", "manual_003")), new byte[] { 9, 9 });                  // only world.bak
            Directory.CreateDirectory(paths.SlotDir(Slot("p001", "quick")));
            File.WriteAllText(paths.MetaFile(Slot("p001", "quick")), "{ broken");                                   // nothing readable
            Directory.CreateDirectory(paths.SlotDir(Slot("p001", "auto_0")));
            File.WriteAllText(paths.MetaFile(Slot("p001", "auto_0")), "{\"MetaVersion\":2,\"Future\":true}");       // newer format
            Directory.CreateDirectory(Path.Combine(paths.ProfileDir(new ProfileId("p001")), "notaslot"));

            var slots = svc.ListSlots(new ProfileId("p001"));
            var byId = slots.ToDictionary(s => s.Ref.Slot.Value);
            CollectionAssert.AreEquivalent(new[] { "manual_001", "manual_002", "manual_003", "quick", "auto_0" }, byId.Keys);
            Assert.AreEqual(SlotStatus.Ok, byId["manual_001"].Status); Assert.AreEqual(t, byId["manual_001"].Meta.SavedUtc); Assert.AreEqual(3, byId["manual_001"].WorldBytes);
            Assert.AreEqual(SlotStatus.MetaMissing, byId["manual_002"].Status); Assert.AreEqual(1, byId["manual_002"].WorldBytes);
            Assert.AreEqual(SlotStatus.BackupOnly, byId["manual_003"].Status); Assert.AreEqual(2, byId["manual_003"].WorldBytes);
            Assert.AreEqual(SlotStatus.Corrupt, byId["quick"].Status); Assert.IsNull(byId["quick"].Meta);
            Assert.AreEqual(SlotStatus.NewerFormat, byId["auto_0"].Status);
            Assert.AreEqual(SlotKind.Quick, byId["quick"].Kind); Assert.AreEqual(SlotKind.Auto, byId["auto_0"].Kind); Assert.AreEqual(SlotKind.Manual, byId["manual_002"].Kind);
            Assert.AreEqual("manual_001", slots[0].Ref.Slot.Value, "slots with a meta come first");
            CollectionAssert.AreEqual(new[] { "auto_0", "manual_002", "manual_003", "quick" }, slots.Skip(1).Select(s => s.Ref.Slot.Value).ToArray(), "then by slot id");
            Assert.AreEqual(0, svc.ListSlots(new ProfileId("p404")).Count, "missing profile: empty, no exception");
        }

        [Test]
        public void NextAutosaveSlot_Rotates()
        {
            var svc = Service();
            var p = new ProfileId("p001");
            var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual("auto_0", svc.NextAutosaveSlot(p).Slot.Value);
            WriteMeta(Slot("p001", "auto_0"), t);
            Assert.AreEqual("auto_1", svc.NextAutosaveSlot(p).Slot.Value);
            WriteMeta(Slot("p001", "auto_1"), t.AddMinutes(5));
            Assert.AreEqual("auto_2", svc.NextAutosaveSlot(p).Slot.Value);
            WriteMeta(Slot("p001", "auto_2"), t.AddMinutes(10));
            Assert.AreEqual("auto_0", svc.NextAutosaveSlot(p).Slot.Value, "oldest");
            WriteMeta(Slot("p001", "auto_0"), t.AddMinutes(15));
            Assert.AreEqual("auto_1", svc.NextAutosaveSlot(p).Slot.Value);
            WriteMeta(Slot("p001", "auto_1"), t.AddMinutes(10));
            Assert.AreEqual("auto_1", svc.NextAutosaveSlot(p).Slot.Value, "tie with auto_2 → lowest index");
        }

        [Test]
        public void ManualSlot_NumberingAndLimit()
        {
            var svc = Service(new SaveServiceConfig { MaxManualSlots = 6 });
            var p = new ProfileId("p001");
            Assert.IsTrue(svc.TryCreateManualSlot(p, out var first)); Assert.AreEqual("manual_001", first.Slot.Value);
            Assert.IsFalse(Directory.Exists(paths.SlotDir(first)), "no folder is created");
            Directory.CreateDirectory(paths.SlotDir(Slot("p001", "manual_002")));
            Directory.CreateDirectory(paths.SlotDir(Slot("p001", "manual_005")));
            Assert.IsTrue(svc.TryCreateManualSlot(p, out var next)); Assert.AreEqual("manual_006", next.Slot.Value, "after the highest number");
            Directory.CreateDirectory(paths.SlotDir(next));
            Assert.IsFalse(svc.TryCreateManualSlot(p, out var none), "limit reached"); Assert.AreEqual(default(SlotRef), none);
            Assert.AreEqual("quick", svc.QuickSlot(p).Slot.Value);
        }

        [Test]
        public void DeleteSlot_ClearsCurrentSlot()
        {
            var svc = Service();
            var s = Slot("p001", "manual_001");
            WriteMeta(s, DateTime.UtcNow);
            var other = Slot("p001", "manual_002");
            WriteMeta(other, DateTime.UtcNow);
            svc.MarkCurrentSlot(s);
            Assert.IsTrue(svc.HasCurrentSlot); Assert.AreEqual(s, svc.CurrentSlot);
            Assert.IsTrue(svc.DeleteSlot(other)); Assert.IsTrue(svc.HasCurrentSlot, "another slot leaves CurrentSlot alone");
            Assert.IsTrue(svc.DeleteSlot(s)); Assert.IsFalse(svc.HasCurrentSlot);
            Assert.IsFalse(Directory.Exists(paths.SlotDir(s)));
            Assert.IsFalse(svc.DeleteSlot(s), "already deleted");
        }
    }
}
