using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Audio;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Presentation.Audio
{
    /// <summary>AUD-007: disposable production importer fixture; no live content database or ownership manifest writes.</summary>
    public abstract class AudioImportFixture
    {
        protected const string Root = "Assets/Game/Content/_Aud007Test";
        protected MigrationManifest Manifest;
        protected string TempRoot;
        bool ownsRoot, ownsTemp;
        [SetUp] public void SetUp()
        {
            Assert.IsFalse(Directory.Exists(Root) || AssetDatabase.IsValidFolder(Root), "Preserve pre-existing user content.");
            ownsRoot = true; Manifest = new MigrationManifest();
            TempRoot = Path.Combine("Temp/TestRuns", "AUD007_" + Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(TempRoot)); ownsTemp = true; Directory.CreateDirectory(TempRoot);
        }
        [TearDown] public void TearDown()
        {
            if (ownsRoot) AssetDatabase.DeleteAsset(Root); ownsRoot = false;
            if (ownsTemp && Directory.Exists(TempRoot)) Directory.Delete(TempRoot, true); ownsTemp = false;
        }
        protected ValidationReport Import(string source = LegacyAudioCueImporter.SourcePath, MigrationManifest migrated = null)
        {
            var report = new ValidationReport("AUD-007 fixture"); LegacyAudioCueImporter.Import(Root, Manifest, report, source, migrated); return report;
        }
        protected AudioCueDefinition Cue(string id) => AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(Root + "/Audio/" + id + ".asset");
        protected AudioConfigDefinition Config => AssetDatabase.LoadAssetAtPath<AudioConfigDefinition>(Root + "/Audio/config_audio.asset");
        protected static void Pass(ValidationReport report) => Assert.IsFalse(report.HasErrors, report.ToText());
        protected string Source(string text)
        { string path = Path.Combine(TempRoot, "AudioLibrary.json"); File.WriteAllText(path, text); return path; }
        protected static string ReplaceFirst(string text, string before, string after)
        {
            int index = text.IndexOf(before, StringComparison.Ordinal); Assert.GreaterOrEqual(index, 0);
            return text.Substring(0, index) + after + text.Substring(index + before.Length);
        }
    }

    /// <summary>SPEC-64 AUD-007 required legacy table and byte/GUID idempotence acceptance.</summary>
    public sealed class LegacyAudioImportTests : AudioImportFixture
    {
        [Test] public void Import_Creates20CuesWithTableValues()
        {
            // The same required import fixture covers fail-closed strict reading, missing clips and Blocked provenance.
            string original = File.ReadAllText(LegacyAudioCueImporter.SourcePath);
            var strict = Import(Source(original.Replace("\"Sounds\":", "\"UnknownField\": 1, \"Sounds\":")));
            Assert.IsTrue(strict.Entries.Any(e => e.Code == "MIG-E002"), strict.ToText()); Assert.IsFalse(Directory.Exists(Root));
            string missing = ReplaceFirst(original, "INTERFACE-MetallicClick.wav", "missing-aud007-first.wav");
            missing = ReplaceFirst(missing, "UIHover_INTERFACE-Positive Click_HY_PC-002.wav", "missing-aud007-second.wav");
            var failures = Import(Source(missing));
            foreach (string file in new[] { "missing-aud007-first.wav", "missing-aud007-second.wav" })
                Assert.IsTrue(failures.Entries.Any(e => e.Code == "AUD-E031" && e.Message.Contains(file)), failures.ToText());
            Assert.IsFalse(Directory.Exists(Root));
            var blocked = Import(Source(ReplaceFirst(original, "INTERFACE-MetallicClick.wav", "ComfyUI_00018_.mp3")));
            Assert.IsTrue(blocked.Entries.Any(e => e.Code == "AUD-E032"), blocked.ToText()); Assert.IsFalse(Directory.Exists(Root));
            Pass(Import());
            var expected = new (string Id, string Clip, float Volume, AudioBus Bus, int Max, int Priority, float Cooldown)[] {
                ("cue_ui_click", "INTERFACE-MetallicClick.wav", 1, AudioBus.Ui, 4, 64, .03f),
                ("cue_ui_hover", "UIHover_INTERFACE-Positive Click_HY_PC-002.wav", 1, AudioBus.Ui, 2, 96, .05f),
                ("cue_ui_slot_click", "INTERFACE-MetallicClick.wav", 1, AudioBus.Ui, 4, 64, .03f),
                ("cue_ui_slot_hover", "UIHover_INTERFACE-Positive Click_HY_PC-002.wav", .1f, AudioBus.Ui, 2, 96, .05f),
                ("cue_ui_view_open", "UIClick_INTERFACE-OpenClose.wav", .5f, AudioBus.Ui, 2, 64, .1f),
                ("cue_ui_view_close", "UIClick_INTERFACE-OpenClose.wav", .5f, AudioBus.Ui, 2, 64, .1f),
                ("cue_ui_fail", "Failure Stinger.wav", .3f, AudioBus.Ui, 1, 32, .3f),
                ("cue_ui_success", "Success Stinger.wav", .3f, AudioBus.Ui, 1, 32, .3f),
                ("cue_weapon_laser_fire", "Projectile_EXPLOSION-Bit Bomb_HY_PC-001.wav", .2f, AudioBus.Sfx, 6, 128, .05f),
                ("cue_weapon_plasma_fire", "EXPLOSION-Cruncher_HY_PC-003.wav", .1f, AudioBus.Sfx, 6, 128, .05f),
                ("cue_weapon_cannon_fire", "EXPLOSION-Cruncher_HY_PC-003.wav", .3f, AudioBus.Sfx, 6, 128, .05f),
                ("cue_impact_hull", "explosion_hull_impact.wav", .3f, AudioBus.Sfx, 8, 140, .04f),
                ("cue_explosion_small", "EXPLOSION-Cruncher_HY_PC-003.wav", .5f, AudioBus.Sfx, 6, 100, .05f),
                ("cue_explosion_large", "EXPLOSION-Cruncher_HY_PC-003.wav", .3f, AudioBus.Sfx, 4, 80, .1f),
                ("cue_editor_place", "Rooted_Dirt_break3_placeholder.ogg", 1, AudioBus.Ui, 4, 96, .03f),
                ("cue_editor_remove", "Rooted_Dirt_break3_placeholder.ogg", 1, AudioBus.Ui, 4, 96, .03f),
                ("cue_music_combat", "10 Boarding the Mechanism.wav", .1f, AudioBus.Music, 1, 0, 0),
                ("cue_music_exploration", "02 The Restricted Hangar.wav", .1f, AudioBus.Music, 1, 0, 0),
                ("cue_music_menu", "02 The Restricted Hangar.wav", .1f, AudioBus.Music, 1, 0, 0),
                ("cue_music_station", "03 Promising Development.wav", .1f, AudioBus.Music, 1, 0, 0),
            };
            Assert.AreEqual(20, expected.Length);
            Assert.AreEqual(30, AssetDatabase.FindAssets("t:AudioCueDefinition", new[] { Root }).Length);
            foreach (var row in expected)
            {
                var cue = Cue(row.Id); Assert.NotNull(cue, row.Id); Assert.AreEqual(row.Id, cue.IdString);
                Assert.AreEqual(1, cue.Clips.Count); Assert.AreEqual(row.Clip, Path.GetFileName(AssetDatabase.GetAssetPath(cue.Clips[0])));
                Assert.AreEqual(row.Volume, cue.Volume); Assert.AreEqual(row.Bus, cue.Bus); Assert.AreEqual(row.Max, cue.MaxVoices);
                Assert.AreEqual(row.Priority, cue.Priority); Assert.AreEqual(row.Cooldown, cue.Cooldown);
                Assert.AreEqual(row.Bus == AudioBus.Sfx ? 1 : 0, cue.SpatialBlend); Assert.AreEqual(row.Bus == AudioBus.Sfx, cue.CombatSignal);
                Assert.AreEqual(row.Bus == AudioBus.Music, cue.Loop);
                Assert.AreEqual(row.Bus == AudioBus.Music ? CoalesceMode.None : row.Bus == AudioBus.Ui ? CoalesceMode.PerCue : CoalesceMode.PerCell, cue.Coalesce);
            }
            foreach (var row in new[] { ("cue_impact_energy", .25f, 1.3f), ("cue_impact_explosive", .4f, 1f), ("cue_impact_ram", .6f, .8f),
                ("cue_shield_hit", .2f, 1.5f), ("cue_cell_break", .4f, .7f), ("cue_break_off", .5f, .8f), ("cue_ship_death", .8f, 1f),
                ("cue_warp", .6f, 1f), ("cue_ui_trade", .5f, 1f), ("cue_stinger_discovery", .5f, 1f) })
            { var cue = Cue(row.Item1); Assert.NotNull(cue); Assert.AreEqual(row.Item2, cue.Volume); Assert.AreEqual(row.Item3, cue.Pitch); }
        }
        [Test] public void Import_IsIdempotent()
        {
            // Exercise the real MIG-011 ownership handoff, then prove its converter cannot downgrade the upgraded cues.
            var migrated = new MigrationManifest(); var migrationReport = new ValidationReport("MIG-011 baseline");
            var index = LegacyContentIndex.Load(Path.GetFullPath("Import/Legacy"), migrationReport); Pass(migrationReport);
            var migrationWriter = new LegacyAssetWriter(Root, migrated, false, migrationReport);
            var context = new LegacyMigrationContext(index, LegacyIdMap.Default, migrationWriter, migrationReport, false, Path.GetFullPath("Import/Legacy"));
            new AudioCueConverter().Convert(context); Pass(migrationReport); Assert.AreEqual(20, migrationWriter.Outcomes.Count);
            string migrationPath = Path.Combine(TempRoot, "legacy-manifest.json"); migrated.Save(migrationPath); byte[] oldOwnership = File.ReadAllBytes(migrationPath);
            Pass(Import(migrated: migrated)); migrated.Save(migrationPath); CollectionAssert.AreEqual(oldOwnership, File.ReadAllBytes(migrationPath));
            string manifestPath = Path.Combine(TempRoot, "audio-manifest.json"); Manifest.Save(manifestPath);
            var paths = Directory.GetFiles(Root + "/Audio", "*.asset").Select(p => p.Replace('\\', '/')).ToArray(); Assert.AreEqual(31, paths.Length);
            var bytes = paths.ToDictionary(p => p, File.ReadAllBytes); var guids = paths.ToDictionary(p => p, AssetDatabase.AssetPathToGUID);
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            Manifest = MigrationManifest.Load(manifestPath); Pass(Import()); Manifest.Save(manifestPath);
            foreach (var path in paths) { CollectionAssert.AreEqual(bytes[path], File.ReadAllBytes(path), path); Assert.AreEqual(guids[path], AssetDatabase.AssetPathToGUID(path), path); }
            CollectionAssert.AreEqual(manifestBytes, File.ReadAllBytes(manifestPath));
            var repeatMigration = new ValidationReport("MIG-011 protected rerun"); var repeatWriter = new LegacyAssetWriter(Root, migrated, false, repeatMigration);
            new AudioCueConverter().Convert(new LegacyMigrationContext(index, LegacyIdMap.Default, repeatWriter, repeatMigration, false, Path.GetFullPath("Import/Legacy")));
            Pass(repeatMigration); Assert.AreEqual(20, repeatWriter.Outcomes.Count);
            Assert.IsTrue(repeatWriter.Outcomes.All(o => o.Outcome == WriteOutcome.SkippedHandEdited));
            foreach (var path in paths) CollectionAssert.AreEqual(bytes[path], File.ReadAllBytes(path), path);
            // Ownership proof cannot silently adopt a later hand edit; no other assets change on a rejected rerun.
            var edited = Cue("cue_ui_click"); using (var so = new SerializedObject(edited))
            { so.FindProperty("volume").floatValue = .42f; so.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssetIfDirty(edited); string editedPath = AssetDatabase.GetAssetPath(edited); bytes[editedPath] = File.ReadAllBytes(editedPath);
            var rejected = Import(); Assert.IsTrue(rejected.Entries.Any(e => e.Code == "AUD-E034"), rejected.ToText());
            foreach (var path in paths) CollectionAssert.AreEqual(bytes[path], File.ReadAllBytes(path), path);
            Assert.AreEqual(.42f, edited.Volume);
        }
    }
}
