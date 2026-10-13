using System;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;

namespace Game.Editor.Content.Validators
{
    /// <summary>SPEC-32 §5.9: travel config cardinality, gate interactions, arrival geometry and ineffective authored gates.</summary>
    public sealed class TravelContentValidator : IContentDatabaseValidator
    {
        public int Order => 320;
        public void Validate(ContentCatalog catalog, ValidationReport report)
        {
            var configs = catalog.All<TravelConfigDefinition>();
            if (configs.Count != 1)
                report.Error("TRV-E001", "Assets/Game/Content/Config", "", "Exactly one TravelConfigDefinition (config_travel) is required; found " + configs.Count + ".");
            else if (configs[0].IdString != "config_travel")
                report.Error("TRV-E001", Path(configs[0]), "id", "Travel config must have ID config_travel.");
            var config = configs.Count == 1 ? configs[0] : null;
            var voxelConfigs = catalog.All<VoxelConfigDefinition>();
            foreach (var gate in catalog.All<BodyTemplateDefinition>())
            {
                if (gate.Role != BodyRole.LaneGate) continue;
                string path = Path(gate);
                Check(report, "TRV-E005", path, "interaction", gate.Interaction == InteractionKind.LaneGate, "Lane gate interaction must be LaneGate.");
                Check(report, "TRV-E005", path, "destructible", !gate.Destructible, "Lane gates must be indestructible.");
                Check(report, "TRV-E005", path, "interactionDistanceM", float.IsFinite(gate.InteractionDistanceM) && gate.InteractionDistanceM >= 50, "Lane gate interaction distance must be finite and at least 50 metres.");
                Check(report, "TRV-E005", path, "interactPrompt", !string.IsNullOrWhiteSpace(gate.InteractPrompt), "Lane gate prompt must be nonempty.");
                // E004 owns invalid config inputs; avoid misleading secondary geometry errors until those are corrected.
                if (config != null && float.IsFinite(config.ArrivalStandoffM) && config.ArrivalStandoffM >= 50 && config.ArrivalStandoffM <= 2000 &&
                    float.IsFinite(config.PlayerClearanceM) && config.PlayerClearanceM >= 0 && config.PlayerClearanceM <= config.ArrivalStandoffM)
                    foreach (var template in catalog.All<StarSystemTemplateDefinition>())
                        Check(report, "TRV-E006", path, "radiusMaxM", float.IsFinite(gate.RadiusMaxM) && float.IsFinite(template.LaneRingMarginM) &&
                            (double)gate.RadiusMaxM + config.ArrivalStandoffM + config.PlayerClearanceM < template.LaneRingMarginM,
                            "Gate radius plus arrival standoff and clearance must be strictly less than " + template.IdString + ".laneRingMarginM.");
                if (gate.StructureSource == StructureSource.Blueprint)
                {
                    var blueprint = gate.Blueprint;
                    double radius = double.NaN;
                    if (blueprint != null && float.IsFinite(blueprint.CellSize) && blueprint.CellSize > 0)
                    {
                        var min = blueprint.BoundsMin; var max = blueprint.BoundsMax;
                        double x = Math.Max(Math.Abs((double)min.x), Math.Abs((double)max.x + 1));
                        double y = Math.Max(Math.Abs((double)min.y), Math.Abs((double)max.y + 1));
                        double z = Math.Max(Math.Abs((double)min.z), Math.Abs((double)max.z + 1));
                        radius = blueprint.CellSize * Length(x, y, z);
                    }
                    Check(report, "TRV-E007", path, "blueprint", double.IsFinite(radius) && float.IsFinite(gate.RadiusMinM) && radius <= gate.RadiusMinM,
                        "Blueprint bounding radius must fit the gate's minimum radius.");
                }
                else if (gate.StructureSource == StructureSource.ProceduralShape)
                {
                    var size = gate.Shape.sizeCells;
                    double radius = voxelConfigs.Count == 1 && float.IsFinite(voxelConfigs[0].DefaultCellSize) && voxelConfigs[0].DefaultCellSize > 0
                        ? voxelConfigs[0].DefaultCellSize * Length(size.x, size.y, size.z) : double.NaN;
                    Check(report, "TRV-E007", path, "shape.sizeCells", double.IsFinite(radius) && float.IsFinite(gate.RadiusMinM) && radius <= gate.RadiusMinM,
                        "Procedural shape needs one valid voxel config and its bounding radius must fit the gate's minimum radius.");
                }
            }
            foreach (var galaxy in catalog.All<GalaxyGenerationDefinition>())
            {
                if (galaxy.AuthoredLanes == null) continue;
                for (int i = 0; i < galaxy.AuthoredLanes.Count; i++)
                {
                    var condition = galaxy.AuthoredLanes[i]?.condition;
                    if (condition != null && !string.IsNullOrEmpty(condition.Source) && condition.IsAlwaysTrue)
                        report.Warning("TRV-W001", Path(galaxy), "authoredLanes[" + i + "].condition", "Authored lane gate condition is always true and has no effect.");
                }
            }
        }
        static double Length(double x, double y, double z) => Math.Sqrt(x * x + y * y + z * z);
        static void Check(ValidationReport report, string code, string path, string field, bool valid, string message)
        { if (!valid) report.Error(code, path, field, message); }
        static string Path(ContentDefinition definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(path) ? "content:" + definition.IdString : path;
        }
    }
}
