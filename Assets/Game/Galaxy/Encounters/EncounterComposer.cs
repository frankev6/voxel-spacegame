using System.Collections.Generic;
using Game.Data.Definitions;

namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4/5.5 composition surface; ENC-006 supplies the deterministic algorithm.</summary>
    public static class EncounterComposer
    {
        // TODO(ENC-006): clear/fill the output with the specified RNG draw order and blueprint radii.
        public static void Compose(EncounterDefinition def, ulong instanceSeed, float defaultCellSize, List<PlannedShip> into)
            => throw new System.NotImplementedException("ENC-006");
        public static float BlueprintRadiusM(ShipBlueprintDefinition bp) => throw new System.NotImplementedException("ENC-006");
    }
}
