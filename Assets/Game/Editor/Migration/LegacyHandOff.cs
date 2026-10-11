using System.Collections.Generic;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.7. Typed legacy data that other specs convert. Built once per run, after this spec's own converters.</summary>
    public sealed class LegacyHandOff
    {
        public static LegacyHandOff Build(LegacyContentIndex index, LegacyIdMap map, Game.Data.Validation.ValidationReport report) { throw new System.NotImplementedException("MIG-015"); }
        public IReadOnlyList<LegacyBodyEncounterLink> BodyEncounterLinks => throw new System.NotImplementedException("MIG-015");
        public IReadOnlyList<LegacyFleetTemplate> Fleets => throw new System.NotImplementedException("MIG-015");
        public IReadOnlyList<LegacyStarSystemTemplate> StarSystems => throw new System.NotImplementedException("MIG-015");
        public Game.Editor.Migration.Dto.LegacyGalaxyDataDto Galaxy => throw new System.NotImplementedException("MIG-015");
        public Game.Editor.Migration.Dto.LegacyOpenWorldGalaxyConfigDto OpenWorld => throw new System.NotImplementedException("MIG-015");
        public const string NarrativeFolder = "narrative";   // Import/Legacy/narrative/*.json (authoritative narrative source)
    }
}
