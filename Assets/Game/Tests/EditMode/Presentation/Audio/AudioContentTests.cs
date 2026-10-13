using System.Linq;
using Game.Data.Validation;
using Game.Editor.Audio;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Presentation.Audio
{
    /// <summary>SPEC-64 AUD-007 required config and blocked-clip acceptance.</summary>
    public sealed class AudioContentTests : AudioImportFixture
    {
        [Test] public void Config_PassesValidate()
        {
            Pass(Import()); Assert.NotNull(Config);
            var report = new ValidationReport("Audio content"); Config.Validate(report.For(AssetDatabase.GetAssetPath(Config)), null); Pass(report);
            CollectionAssert.AreEqual(new[] { "cue_ui_hover", "cue_ui_click", "cue_ui_view_open", "cue_ui_view_close", "cue_ui_view_close", "cue_ui_fail",
                "cue_ui_success", "cue_ui_slot_hover", "cue_ui_slot_click", "cue_ui_success", "cue_ui_click" }, Config.UiSounds.Select(c => c.IdString));
            Assert.AreSame(Cue("cue_music_menu"), Config.MusicMenu); Assert.AreSame(Cue("cue_music_exploration"), Config.MusicExploration);
            Assert.AreSame(Cue("cue_music_combat"), Config.MusicCombat); Assert.AreSame(Cue("cue_music_station"), Config.MusicEditor);
            Assert.IsNull(Config.AmbienceWorld); Assert.IsNull(Config.EngineLoop); Assert.IsNull(Config.BeamLoop);
            Assert.AreSame(Cue("cue_impact_hull"), Config.ImpactKinetic); Assert.AreSame(Cue("cue_impact_energy"), Config.ImpactEnergy);
            Assert.AreSame(Cue("cue_impact_explosive"), Config.ImpactExplosive); Assert.AreSame(Cue("cue_impact_ram"), Config.ImpactRam);
            Assert.AreSame(Cue("cue_explosion_small"), Config.ExplosionSmall); Assert.AreSame(Cue("cue_explosion_large"), Config.ExplosionLarge);
            Assert.AreSame(Cue("cue_shield_hit"), Config.ShieldHit); Assert.AreSame(Cue("cue_cell_break"), Config.CellBreak);
            Assert.AreSame(Cue("cue_break_off"), Config.BreakOff); Assert.AreSame(Cue("cue_ship_death"), Config.ShipDeath);
        }
        [Test] public void NoCueReferencesBlockedClips()
        {
            Pass(Import()); var table = AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath); Assert.NotNull(table);
            var blocked = table.entries.Where(e => e != null && e.status == ProvenanceStatus.Blocked).Select(e => e.clip).ToArray();
            Assert.AreEqual(2, blocked.Length);
            foreach (string guid in AssetDatabase.FindAssets("t:AudioCueDefinition", new[] { Root }))
            {
                var cue = AssetDatabase.LoadAssetAtPath<Game.Data.Definitions.AudioCueDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var clip in cue.Clips) { CollectionAssert.DoesNotContain(blocked, clip, cue.IdString); Assert.NotNull(clip); }
                var report = new ValidationReport(cue.IdString); cue.Validate(report.For(AssetDatabase.GetAssetPath(cue)), null); Pass(report);
            }
        }
    }
}
