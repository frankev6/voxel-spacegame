using System.IO;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-002: the folder layout of SPEC-05 §4.1.</summary>
    public sealed class SavePathsTests
    {
        [Test]
        public void Layout_MatchesSpec()
        {
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "saves_layout"));
            var p = new SavePaths(root);
            var profile = new ProfileId("p001");
            var slot = new SlotRef(profile, new SlotId("manual_001"));
            Assert.AreEqual(root, p.Root);
            Assert.AreEqual(Path.Combine(root, "p001"), p.ProfileDir(profile));
            Assert.AreEqual(Path.Combine(root, "p001", "profile.json"), p.ProfileFile(profile));
            Assert.AreEqual(Path.Combine(root, "p001", "manual_001"), p.SlotDir(slot));
            Assert.AreEqual(Path.Combine(root, "p001", "manual_001", "world.sav"), p.WorldFile(slot));
            Assert.AreEqual(Path.Combine(root, "p001", "manual_001", "world.bak"), p.BackupFile(slot));
            Assert.AreEqual(Path.Combine(root, "p001", "manual_001", "world.sav.tmp"), p.TempFile(slot));
            Assert.AreEqual(Path.Combine(root, "p001", "manual_001", "meta.json"), p.MetaFile(slot));
            Assert.AreEqual(Path.Combine(root, "p001", "manual_001", "thumbs"), p.ThumbsDir(slot));
            Assert.AreEqual(Path.Combine(root, "p001", "manual_001", "thumbs", "slot.png"), p.ThumbnailFile(slot, "slot"));
            StringAssert.EndsWith(Path.Combine("saves"), SavePaths.CreateDefault().Root);
            Assert.Throws<System.ArgumentException>(() => new SavePaths(""));
        }
    }
}
