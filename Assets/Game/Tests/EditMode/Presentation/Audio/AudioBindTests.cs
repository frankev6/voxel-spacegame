using System.Linq;
using Game.Core.Audio;
using Game.Data;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Presentation.Audio
{
    /// <summary>SPEC-64 AUD-007: boot cue hashes and create/set behavior in isolated worlds.</summary>
    public sealed class AudioBindTests : AudioImportFixture
    {
        [Test] public void BindTo_CreatesCombatAudioCues()
        {
            Pass(Import()); var parent = new GameObject("AudioBindTests"); var database = ScriptableObject.CreateInstance<ContentDatabase>();
            AudioService service = null;
            using var world = new World("AudioBindTests");
            try
            {
                database.EditorSetDefinitions(AssetDatabase.FindAssets("t:AudioCueDefinition", new[] { Root })
                    .Select(g => (ContentDefinition)AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(c => c.IdString).ToList());
                service = new AudioService(Config, database, AudioVolumes.Default, parent.transform); service.BindTo(world);
                using var query = world.EntityManager.CreateEntityQuery(typeof(CombatAudioCues)); Assert.AreEqual(1, query.CalculateEntityCount());
                var entity = query.GetSingletonEntity(); Check(query.GetSingleton<CombatAudioCues>());
                world.EntityManager.SetComponentData(entity, default(CombatAudioCues)); service.BindTo(world);
                Assert.AreEqual(entity, query.GetSingletonEntity()); Check(query.GetSingleton<CombatAudioCues>());
                service.Unbind(world); service.BindTo(world); Assert.AreEqual(1, query.CalculateEntityCount()); Check(query.GetSingleton<CombatAudioCues>());
                service.Dispose(); service = new AudioService(null, database, AudioVolumes.Default, parent.transform); service.BindTo(world);
                var silent = query.GetSingleton<CombatAudioCues>(); Assert.AreEqual(0UL, silent.ImpactKinetic.Value); Assert.AreEqual(0UL, silent.ShipDeath.Value);
            }
            finally { service?.Dispose(); Object.DestroyImmediate(parent); Object.DestroyImmediate(database); }

            void Check(CombatAudioCues value)
            {
                Assert.AreEqual(Config.ImpactKinetic.Id.Hash, value.ImpactKinetic); Assert.AreEqual(Config.ImpactEnergy.Id.Hash, value.ImpactEnergy);
                Assert.AreEqual(Config.ImpactExplosive.Id.Hash, value.ImpactExplosive); Assert.AreEqual(Config.ImpactRam.Id.Hash, value.ImpactRam);
                Assert.AreEqual(Config.ExplosionSmall.Id.Hash, value.ExplosionSmall); Assert.AreEqual(Config.ExplosionLarge.Id.Hash, value.ExplosionLarge);
                Assert.AreEqual(Config.ShieldHit.Id.Hash, value.ShieldHit); Assert.AreEqual(Config.CellBreak.Id.Hash, value.CellBreak);
                Assert.AreEqual(Config.BreakOff.Id.Hash, value.BreakOff); Assert.AreEqual(Config.ShipDeath.Id.Hash, value.ShipDeath);
            }
        }
    }
}
