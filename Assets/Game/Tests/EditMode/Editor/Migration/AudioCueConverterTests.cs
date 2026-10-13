using System;
using System.IO;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Audio;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using Game.Editor.Migration.Dto;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>SPEC-72 MIG-011: real audio export and migration safety in a disposable content root.</summary>
    public sealed class AudioCueConverterTests
    {
        const string Root = "Assets/Game/Content/_AudioCueMigrationTest";
        LegacyContentIndex index;
        MigrationManifest manifest;
        bool ownsRoot;

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root) || Directory.Exists(Root), "Never remove existing user content.");
            ownsRoot = true;
            manifest = new MigrationManifest();
            var report = new ValidationReport("MIG");
            index = LegacyContentIndex.Load(Path.GetFullPath("Import/Legacy"), report);
            Assert.IsFalse(report.HasErrors, report.ToText());
        }

        [TearDown] public void Clean()
        {
            if (ownsRoot) AssetDatabase.DeleteAsset(Root);
            ownsRoot = false;
        }

        LegacyMigrationContext Convert()
        {
            var report = new ValidationReport("MIG");
            var writer = new LegacyAssetWriter(Root, manifest, false, report);
            var context = new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, false, Path.GetFullPath("Import/Legacy"));
            new AudioCueConverter().Convert(context);
            return context;
        }

        [Test] public void RealExport_TwentyCuesWithClips()
        {
            var context = Convert();
            Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
            Assert.AreEqual(20, context.Writer.Outcomes.Count);
            var expectedIds = new[]
            {
                "cue_ui_click", "cue_ui_hover", "cue_ui_slot_click", "cue_ui_slot_hover", "cue_ui_view_open", "cue_ui_view_close", "cue_ui_fail", "cue_ui_success",
                "cue_weapon_laser_fire", "cue_weapon_plasma_fire", "cue_weapon_cannon_fire", "cue_impact_hull", "cue_explosion_small", "cue_explosion_large", "cue_editor_place", "cue_editor_remove",
                "cue_music_combat", "cue_music_exploration", "cue_music_menu", "cue_music_station"
            };
            CollectionAssert.AreEquivalent(expectedIds, context.Writer.Outcomes.Select(o => Path.GetFileNameWithoutExtension(o.AssetPath)));
            var dto = LegacyJson.Read<LegacyAudioLibraryDto>(index.OfType("AudioLibrary").Single(), context.Report);
            Assert.NotNull(dto);
            foreach (var entry in dto.Sounds) CheckCue(context, entry.Id, entry.Clip, entry.Volume, AudioBus.Sfx);
            foreach (var entry in dto.Music) CheckCue(context, entry.Track, entry.Clip, entry.Volume, AudioBus.Music);
            Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
            var converter = new AudioCueConverter();
            Assert.AreEqual("Audio", converter.Name);
            Assert.AreEqual(200, converter.Order);
        }

        static void CheckCue(LegacyMigrationContext context, string key, LegacyRef reference, double volume, AudioBus bus)
        {
            Assert.IsTrue(context.IdMap.TryMap(LegacyIdKind.AudioCue, key, out string id), key);
            string path = Root + "/Audio/" + id + ".asset";
            var cue = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(path);
            Assert.NotNull(cue, id);
            Assert.AreEqual(id, cue.IdString);
            Assert.AreEqual(1, cue.Clips.Count, id);
            Assert.NotNull(cue.Clips[0], id);
            Assert.AreEqual("Assets/Game/Audio/" + (bus == AudioBus.Music ? "Music/" : "Sfx/") + Path.GetFileName(reference.Path), AssetDatabase.GetAssetPath(cue.Clips[0]), id);
            Assert.AreEqual((float)volume, cue.Volume, id);
            Assert.AreEqual(bus, cue.Bus, id);
            cue.Validate(context.Report.For(path), new ContentValidationContext(path, true, _ => true));
        }

        [Test] public void SecondRun_Unchanged()
        {
            var first = Convert();
            Assert.IsFalse(first.Report.HasErrors, first.Report.ToText());
            Assert.AreEqual(20, first.Writer.Outcomes.Count);
            var bytes = first.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => File.ReadAllBytes(o.AssetPath));
            var guids = first.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => AssetDatabase.AssetPathToGUID(o.AssetPath));
            var second = Convert();
            Assert.IsFalse(second.Report.HasErrors, second.Report.ToText());
            Assert.AreEqual(20, second.Writer.Outcomes.Count);
            foreach (var outcome in second.Writer.Outcomes)
            {
                Assert.AreEqual(WriteOutcome.Unchanged, outcome.Outcome, outcome.AssetPath);
                CollectionAssert.AreEqual(bytes[outcome.AssetPath], File.ReadAllBytes(outcome.AssetPath), outcome.AssetPath);
                Assert.AreEqual(guids[outcome.AssetPath], AssetDatabase.AssetPathToGUID(outcome.AssetPath), outcome.AssetPath);
            }
        }

        // Load a disposable source fixture rather than requiring Newtonsoft in the test assembly.
        void ReplaceFirstClip(string replacement)
        {
            var file = index.OfType("AudioLibrary").Single();
            var report = new ValidationReport("MIG");
            string original = LegacyJson.Read<LegacyAudioLibraryDto>(file, report).Sounds[0].Clip.Path;
            string text = File.ReadAllText(Path.Combine("Import/Legacy", file.RelativePath));
            int offset = text.IndexOf(original, StringComparison.Ordinal);
            Assert.GreaterOrEqual(offset, 0);
            string temporaryRoot = Path.Combine(Path.GetTempPath(), "AudioMigration_" + Guid.NewGuid().ToString("N"));
            try
            {
                string target = Path.Combine(temporaryRoot, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllText(target, text.Substring(0, offset) + replacement + text.Substring(offset + original.Length));
                index = LegacyContentIndex.Load(temporaryRoot, report);
                Assert.IsFalse(report.HasErrors, report.ToText());
            }
            finally { if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }
        }

        [Test] public void MissingClip_IsE060AndOtherEntriesContinue()
        {
            ReplaceFirstClip("Assets/Game/SFX/missing-migration-clip.wav");
            var context = Convert();
            Assert.AreEqual(19, context.Writer.Outcomes.Count);
            var error = context.Report.Entries.Single(e => e.Severity == ValidationSeverity.Error);
            Assert.AreEqual("MIG-E060", error.Code);
            Assert.AreEqual("Sounds[0].Clip", error.Field);
            StringAssert.Contains("Assets/Game/Audio/Sfx/missing-migration-clip.wav", error.Message);
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(Root + "/Audio/cue_ui_click.asset"));
        }

        [Test] public void BlockedClip_IsRejectedAndOtherEntriesContinue()
        {
            var table = AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath);
            Assert.NotNull(table);
            var blocked = table.entries.First(e => e.status == ProvenanceStatus.Blocked);
            string blockedPath = AssetDatabase.GetAssetPath(blocked.clip);
            ReplaceFirstClip(blockedPath.Replace("Assets/Game/Audio/Music/", "Assets/Game/SFX/Music/").Replace("Assets/Game/Audio/Sfx/", "Assets/Game/SFX/"));
            var context = Convert();
            Assert.AreEqual(19, context.Writer.Outcomes.Count);
            var error = context.Report.Entries.Single(e => e.Severity == ValidationSeverity.Error);
            Assert.AreEqual("MIG-E061", error.Code);
            StringAssert.Contains("Blocked", error.Message);
            StringAssert.Contains(blockedPath, error.Message);
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(Root + "/Audio/cue_ui_click.asset"));
            foreach (var outcome in context.Writer.Outcomes)
                CollectionAssert.DoesNotContain(AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(outcome.AssetPath).Clips, blocked.clip);
        }
    }
}
