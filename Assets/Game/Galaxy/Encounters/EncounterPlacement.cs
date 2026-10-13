using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4/5.6 placement surface; ENC-006 owns clearance/line placement, ENC-013 adds formations.</summary>
    public static class EncounterPlacement
    {
        // TODO(ENC-006): deterministic star-system-frame clearance and line-abreast placement.
        public static bool TryPlaceCentre(StarSystemContent content, double3 anchorM, float anchorRadiusM, double3 playerM,
            double universalTime, ulong instanceSeed, in PlacementParams p, out double3 centreM)
            => throw new System.NotImplementedException("ENC-006");
        public static void ShipPoses(double3 centreM, double3 facePointM, IReadOnlyList<PlannedShip> ships, float spacingM,
            FormationDefinition formation, List<(double3 pos, float yaw)> into) => throw new System.NotImplementedException("ENC-006");
    }
}
