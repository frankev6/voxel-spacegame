// Lead-only: copy to Assets/Game/Editor/GalaxyM3ContentJob.cs, invoke
// Game.Editor.GalaxyM3ContentJob.Run in the serialized Editor, then remove temporary source/meta.
// Assets are authored by GAL-012-author-world.py. This imports, checks actual references and records readback;
// it does not replace owners' pending GAL-004/008, NAR-011 or TRV-003 work.
using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Content.Validators;
using Game.Voxel.Model;
using Unity.Collections;
using UnityEditor;

namespace Game.Editor
{
    public static class GalaxyM3ContentJob
    {
        const string Evidence = "Logs/playtest/gal012-content";
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import requires a stopped Editor.");
            foreach (string folder in new[] { "Assets/Game/Content/Bodies", "Assets/Game/Content/Galaxy" })
                foreach (string path in Directory.GetFiles(folder, "*.asset").OrderBy(p => p, StringComparer.Ordinal))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var database = ContentDatabaseBuilder.Rebuild();
            var report = new ValidationReport("GAL-012 authored content");
            var catalog = ContentCatalog.Build(database.Definitions, report);
            var membership = database.Definitions.ToHashSet();
            foreach (var definition in database.Definitions.Where(d => d is GalaxyGenerationDefinition || d is AuthoredStarSystemDefinition ||
                     d is StarSystemTemplateDefinition || d is BodyTemplateDefinition || d is BodyVisualDefinition))
            {
                string path = AssetDatabase.GetAssetPath(definition);
                definition.Validate(report.For(path), new ContentValidationContext(path, true, membership.Contains, catalog));
            }
            new GalaxyContentValidator().Validate(catalog, report);
            var travelValidator = TypeCache.GetTypesDerivedFrom<IContentDatabaseValidator>()
                .SingleOrDefault(t => t.FullName == "Game.Editor.Content.Validators.TravelContentValidator");
            if (travelValidator == null) throw new InvalidOperationException("Integrate real TRV-002 travel validator/config before strict world readback.");
            ((IContentDatabaseValidator)Activator.CreateInstance(travelValidator)).Validate(catalog, report);
            var generation = catalog.Get<GalaxyGenerationDefinition>(new ContentId("galaxy_main"));
            if (generation.AuthoredStarSystems.Count != 3 || generation.AuthoredLanes.Count != 2 || generation.ProceduralStarSystemCount != 0 || !generation.UseFixedSeed || generation.FixedSeed == 0)
                throw new InvalidOperationException("Opening chapter must contain exactly three authored systems, two lanes and a nonzero fixed seed.");
            var expected = new[] { ("starsys_ark_start", 0f, 0f, 0), ("starsys_drift", 6f, 2f, 2), ("starsys_whisper", -5f, 4f, 1) };
            foreach (var row in expected)
            {
                var system = catalog.Get<AuthoredStarSystemDefinition>(new ContentId(row.Item1));
                if (system.PositionLy.x != row.Item2 || system.PositionLy.y != row.Item3 || system.Danger != row.Item4 || !generation.AuthoredStarSystems.Contains(system))
                    throw new InvalidOperationException("System topology differs from SPEC-42: " + row.Item1);
            }
            if (generation.StartStarSystem.IdString != "starsys_ark_start" || generation.StartStarSystem.StartBodyKey != "ark_station")
                throw new InvalidOperationException("Opening chapter start location differs.");
            var whisper = catalog.Get<AuthoredStarSystemDefinition>(new ContentId("starsys_whisper"));
            var gate = generation.AuthoredLanes.Single(l => l.a == whisper || l.b == whisper);
            if (gate.condition.Source != "$gate_ark_start_active" || gate.lockedMessage != "The gate is dormant." || gate.gateTemplateA?.IdString != "body_gate_dormant" || gate.gateTemplateB?.IdString != "body_gate_dormant")
                throw new InvalidOperationException("Whisper gate source/message/templates differ.");
            var drift = catalog.Get<AuthoredStarSystemDefinition>(new ContentId("starsys_drift"));
            var belt = drift.Bodies.Single(b => b.key == "drift_belt"); var cache = drift.Bodies.Single(b => b.key == "drift_cache");
            if (!(cache.orbitRadiusM < belt.orbitRadiusM + belt.template.BeltWidthM * .5f))
                throw new InvalidOperationException("Drift cache must be inside the belt outer edge.");
            var outpost = whisper.Bodies.Single(b => b.key == "whisper_outpost").template;
            if (outpost.StructureSource != StructureSource.Blueprint || outpost.Blueprint?.IdString != "ship_vestari")
                throw new InvalidOperationException("Outpost requires the existing turret-equipped blueprint.");
            if (!VoxelStructureCodec.TryDecode(outpost.Blueprint.Payload, catalog, out var structure, out string error))
                throw new InvalidOperationException("Outpost blueprint cannot decode: " + error);
            int turrets = 0;
            using (structure)
            using (var blocks = new NativeList<StructureBlock>(Allocator.TempJob))
            {
                structure.GetBlocks(blocks);
                foreach (var placed in blocks)
                {
                    var block = catalog.GetBlock(placed.Placement.Block);
                    if (block == null) throw new InvalidOperationException("Decoded blueprint contains a missing block.");
                    if (block.IdString == "weapon_small_beam_turret" && block.Modules.OfType<WeaponMountModule>().Any(m => m.Weapon != null)) turrets++;
                }
            }
            if (turrets < 1) throw new InvalidOperationException("Outpost blueprint has no real turret instances.");
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/validation.txt", report.ToText(int.MaxValue));
            File.WriteAllText(Evidence + "/readback.txt", string.Join("\n", database.Definitions.Where(d => d is GalaxyGenerationDefinition || d is AuthoredStarSystemDefinition || d is StarSystemTemplateDefinition || d is BodyTemplateDefinition || d is BodyVisualDefinition)
                .OrderBy(d => d.IdString, StringComparer.Ordinal).Select(d => d.IdString + " " + AssetDatabase.GetAssetPath(d) + " " + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(d)))) + "\nOutpost real turret instances: " + turrets + "\n");
            if (report.HasErrors) throw new InvalidOperationException(report.ToText());
            GameLog.Info("Content", report.ToText() + "\nAuthored content read back. GalaxyService.NewGame/body resolution/lane-access acceptance remains pending its feature owners.");
            if (gate.condition.IsStale || gate.condition.HasErrors)
                GameLog.Warn("Content", "Whisper gate remains fail-closed until TRV-003 compiles/binds authored lane conditions; no narrative/runtime success claimed.");
        }
    }
}
