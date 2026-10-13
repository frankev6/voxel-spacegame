// MIG-014 lead-only targeted generation recipe, outside Assets and unexecuted by the worker.
// Copy to Assets/Game/Editor/BodyMigrationJob.cs after integrating MIG-014. With the Editor
// stopped, invoke Game.Editor.Migration.BodyMigrationJob.Run (RunCli for headless execution).
// Remove the temporary script/meta afterwards. Commit the 20 bodies/metas, catalog and
// owned Import/Migration/manifest.json hashes. Evidence: Logs/playtest/mig014/.
// This creates identity stubs only: no visual, structure, belt block or galaxy configuration
// is invented. Global GAL-003/full migration validation remains a separate lead operation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Migration.Dto;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Game.Editor.Migration
{
    public static class BodyMigrationJob
    {
        const string ManifestPath = "Import/Migration/manifest.json";
        const string EvidenceRoot = "Logs/playtest/mig014";
        static readonly (string Id, string Name, BodyRole Role, string Tag)[] Expected =
        {
            ("body_star", "Star", BodyRole.Star, "star"), ("body_star_blackhole", "Black Hole", BodyRole.Star, "black_hole"),
            ("body_planet_barren", "Barren Planet", BodyRole.Planet, "barren_planet"), ("body_planet_gas_giant", "Gas Giant", BodyRole.Planet, "gas_giant"),
            ("body_planet_ice", "Ice Planet", BodyRole.Planet, "ice_planet"), ("body_planet_ice_giant", "Ice Giant", BodyRole.Planet, "ice_giant"),
            ("body_planet_rocky", "Rocky Planet", BodyRole.Planet, "rocky_planet"), ("body_planet_terrestrial", "Terrestrial Planet", BodyRole.Planet, "terrestrial_planet"),
            ("body_planet_water", "Ocean Planet", BodyRole.Planet, "ocean_planet"), ("body_moon", "Moon", BodyRole.Moon, "moon"),
            ("body_station", "Station", BodyRole.Station, "station"), ("body_station_ark", "ARK Station", BodyRole.Station, "ark_station"),
            ("body_station_abandoned", "Abandoned Station", BodyRole.Station, "abandoned_station"), ("body_asteroid_belt", "Asteroid Belt", BodyRole.Belt, "asteroid_belt"),
            ("body_gate_active", "Active Gate", BodyRole.LaneGate, "gate_active"), ("body_gate_dormant", "Dormant Gate", BodyRole.LaneGate, "gate_dormant"),
            ("body_jump_point", "Jump Point", BodyRole.LaneGate, "jump_point"), ("body_derelict_ship", "Derelict Ship", BodyRole.PointOfInterest, "derelict"),
            ("body_debris_field", "Debris Field", BodyRole.PointOfInterest, "debris_field"), ("body_resource_cache", "Resource Cache", BodyRole.PointOfInterest, "resource_cache")
        };

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Body migration requires a stopped Editor.");
            Directory.CreateDirectory(EvidenceRoot);
            string legacyRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Import/Legacy"));
            var preflight = new ValidationReport("MIG-014 preflight");
            var index = LegacyContentIndex.Load(legacyRoot, preflight);
            var manifest = MigrationManifest.Load(ManifestPath);
            var definitions = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>()).ToArray();
            var catalog = ContentCatalog.Build(definitions, preflight); // identity checks, not full world validation
            var sources = new Dictionary<string, LegacySystemEntityDto>(StringComparer.Ordinal);
            var sourceBytes = new Dictionary<string, byte[]>();
            foreach (var file in index.Files)
            {
                var dto = ReadBody(file, preflight); if (dto == null) continue;
                if (!LegacyIdMap.Default.TryMap(LegacyIdKind.Body, dto.Name, out string id) || id == null)
                    LegacyIdMap.Default.TryMap(LegacyIdKind.Body, dto.EntityId, out id);
                if (id == null || sources.ContainsKey(id) || !Expected.Any(e => e.Id == id))
                    preflight.Error("MIG-E003", file.RelativePath, "entityId", "Missing, duplicate or unexpected canonical body identity.");
                else sources.Add(id, dto);
                sourceBytes.Add(Path.Combine(legacyRoot, file.RelativePath), File.ReadAllBytes(Path.Combine(legacyRoot, file.RelativePath)));
            }
            if (sources.Count != 20 || Expected.Any(e => !sources.ContainsKey(e.Id)))
                preflight.Error("MIG-E003", "Import/Legacy", "Bodies", "Expected exactly the twenty documented body sources.");
            var before = new Dictionary<string, (byte[] Bytes, string Guid, bool HasHash, ulong Hash, JObject OtherFields)>();
            foreach (var row in Expected)
            {
                string path = PathOf(row.Id); var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (catalog.TryGet<BodyTemplateDefinition>(new ContentId(row.Id), out var identified) && AssetDatabase.GetAssetPath(identified) != path)
                    preflight.Error("MIG-E010", path, "id", "Identity exists at another path; reconcile rather than duplicate it.");
                if (asset != null && !(asset is BodyTemplateDefinition) || asset == null && File.Exists(path))
                    preflight.Error("MIG-E010", path, "", "Target must be a loadable BodyTemplateDefinition.");
                if (asset == null && File.Exists(path + ".meta"))
                    preflight.Error("MIG-E010", path, "", "Orphaned target metadata needs reconciliation.");
                if (asset is BodyTemplateDefinition body)
                {
                    if (body.IdString != row.Id) preflight.Error("MIG-E010", path, "id", "Target identity differs from its canonical path.");
                    bool hasHash = manifest.TryGetHash(path, out ulong hash);
                    before.Add(path, (File.ReadAllBytes(path), AssetDatabase.AssetPathToGUID(path), hasHash, hash, OtherFields(body)));
                }
            }
            // Bound the write set: every pre-existing content file outside these bodies must remain byte-identical.
            var targets = new HashSet<string>(Expected.Select(e => PathOf(e.Id)), StringComparer.Ordinal);
            var untouched = Directory.GetFiles(ContentDatabaseBuilder.ContentRoot, "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).Where(p => p != ContentDatabaseBuilder.DatabasePath && !targets.Contains(p) && !targets.Contains(p.EndsWith(".meta", StringComparison.Ordinal) ? p.Substring(0, p.Length - 5) : p))
                .ToDictionary(p => p, File.ReadAllBytes);
            File.WriteAllText(EvidenceRoot + "/preflight.txt", preflight.ToText());
            if (preflight.HasErrors) throw new ContentValidationException(preflight);

            var report = new ValidationReport("MIG-014 targeted generation");
            var writer = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, report);
            new Converters.BodyTemplateConverter().Convert(new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, false, legacyRoot) { Catalog = catalog });
            manifest.Save(ManifestPath); ContentDatabaseBuilder.Rebuild();
            VerifyOutcomes(writer, report);
            foreach (var outcome in writer.Outcomes)
            {
                var body = AssetDatabase.LoadAssetAtPath<BodyTemplateDefinition>(outcome.AssetPath);
                if (body == null) { report.Error("MIG-E099", outcome.AssetPath, "", "Missing output."); continue; }
                if (before.TryGetValue(outcome.AssetPath, out var prior))
                {
                    if (prior.Guid != AssetDatabase.AssetPathToGUID(outcome.AssetPath) || !JToken.DeepEquals(prior.OtherFields, OtherFields(body)))
                        report.Error("MIG-E099", outcome.AssetPath, "", "GUID or unrepresented world fields changed.");
                    if (outcome.Outcome == WriteOutcome.SkippedHandEdited)
                    {
                        bool hasHash = manifest.TryGetHash(outcome.AssetPath, out ulong retained);
                        if (!prior.Bytes.SequenceEqual(File.ReadAllBytes(outcome.AssetPath)) || hasHash != prior.HasHash || hasHash && retained != prior.Hash)
                            report.Error("MIG-E099", outcome.AssetPath, "", "Protected bytes/ownership changed.");
                        continue; // Keep authored identities; do not claim their text equals the import.
                    }
                }
                var row = Expected.Single(e => PathOf(e.Id) == outcome.AssetPath); var source = sources[row.Id];
                string description = source.Description == "This is a placeholder description blah blah blah" ? "" : source.Description ?? "";
                if (body.IdString != row.Id || body.DisplayName != row.Name || body.Role != row.Role || body.StoryTag != row.Tag || body.Description != description)
                    report.Error("MIG-E099", outcome.AssetPath, "", "Output differs from the documented stub fields.");
                if (!manifest.TryGetHash(outcome.AssetPath, out ulong owned) || owned != StableHash.Hash64(File.ReadAllBytes(outcome.AssetPath)))
                    report.Error("MIG-E099", outcome.AssetPath, "", "Owned output hash does not match readback.");
            }
            VerifyUntouched(sourceBytes, untouched, report);
            File.WriteAllText(EvidenceRoot + "/targeted-generation.txt", report.ToText());
            File.WriteAllLines(EvidenceRoot + "/outputs.tsv", writer.Outcomes.Select(o => o.AssetPath + "\t" + o.Outcome + "\t" + AssetDatabase.AssetPathToGUID(o.AssetPath)));
            if (report.HasErrors) throw new ContentValidationException(report);

            byte[] ownedManifest = File.ReadAllBytes(ManifestPath);
            var generated = writer.Outcomes.ToDictionary(o => o.AssetPath, o => (Bytes: File.ReadAllBytes(o.AssetPath), Guid: AssetDatabase.AssetPathToGUID(o.AssetPath)));
            var rerun = new ValidationReport("MIG-014 targeted rerun");
            var second = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, rerun);
            new Converters.BodyTemplateConverter().Convert(new LegacyMigrationContext(index, LegacyIdMap.Default, second, rerun, false, legacyRoot));
            manifest.Save(ManifestPath); VerifyOutcomes(second, rerun);
            foreach (var outcome in second.Outcomes)
            {
                var prior = generated[outcome.AssetPath];
                if (outcome.Outcome != WriteOutcome.Unchanged && outcome.Outcome != WriteOutcome.SkippedHandEdited ||
                    !prior.Bytes.SequenceEqual(File.ReadAllBytes(outcome.AssetPath)) || prior.Guid != AssetDatabase.AssetPathToGUID(outcome.AssetPath))
                    rerun.Error("MIG-E099", outcome.AssetPath, "", "Rerun changed bytes, GUID or outcome.");
            }
            if (!ownedManifest.SequenceEqual(File.ReadAllBytes(ManifestPath))) rerun.Error("MIG-E099", ManifestPath, "", "Rerun changed the manifest.");
            VerifyUntouched(sourceBytes, untouched, rerun);
            File.WriteAllText(EvidenceRoot + "/targeted-rerun.txt", rerun.ToText());
            if (rerun.HasErrors) throw new ContentValidationException(rerun);
            GameLog.Info("Migration", "MIG-014: twenty stub outcomes; protected fields, bytes, GUIDs and unchanged rerun verified. Full world validation is separate.");
        }
        public static void RunCli()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); }
        }
        static string PathOf(string id) => ContentDatabaseBuilder.ContentRoot + "/Bodies/" + id + ".asset";
        // Diagnostic snapshot only; no JSON save format. Exclude precisely the four converter-owned fields.
        static JObject OtherFields(BodyTemplateDefinition body)
        {
            var json = JObject.Parse(EditorJsonUtility.ToJson(body));
            foreach (string field in new[] { "displayName", "description", "role", "storyTag" }) json.Remove(field);
            return json;
        }
        static void VerifyOutcomes(LegacyAssetWriter writer, ValidationReport report)
        {
            if (writer.Outcomes.Count != 20 || writer.Outcomes.Select(o => o.AssetPath).Distinct().Count() != 20 ||
                writer.Outcomes.Any(o => o.Outcome == WriteOutcome.Failed || !Expected.Any(e => o.AssetPath == PathOf(e.Id))))
                report.Error("MIG-E099", "ContentDatabase", "Bodies", "Expected exactly twenty successful body outcomes.");
        }
        static void VerifyUntouched(Dictionary<string, byte[]> sources, Dictionary<string, byte[]> otherContent, ValidationReport report)
        {
            foreach (var pair in sources.Concat(otherContent))
                if (!File.Exists(pair.Key) || !pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) report.Error("MIG-E099", pair.Key, "", "Read-only input/unrelated content changed.");
        }
        static LegacySystemEntityDto ReadBody(LegacyFile file, ValidationReport report) => file.Type switch
        {
            "SolarSystemEntity" => LegacyJson.Read<LegacySystemEntityDto>(file, report),
            "PlanetEntity" => LegacyJson.Read<LegacyPlanetEntityDto>(file, report),
            "StarEntity" => LegacyJson.Read<LegacyStarEntityDto>(file, report),
            "Assets.Game.Core.Galaxy_Map.HyperlaneEntity" => LegacyJson.Read<LegacyHyperlaneEntityDto>(file, report),
            _ => null
        };
    }
}
