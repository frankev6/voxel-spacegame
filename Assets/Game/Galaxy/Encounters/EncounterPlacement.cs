using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4/5.6 placement surface; ENC-006 owns clearance/line placement, ENC-013 adds formations.</summary>
    public static class EncounterPlacement
    {
        public static bool TryPlaceCentre(StarSystemContent content, double3 anchorM, float anchorRadiusM, double3 playerM,
            double universalTime, ulong instanceSeed, in PlacementParams p, out double3 centreM)
        {
            centreM = default;
            var rng = RngStreams.For(instanceSeed, RngPurpose.EncounterPlacement, 0);
            float u = rng.NextFloat(), v = rng.NextFloat();
            double distance = (double)anchorRadiusM + math.lerp(p.DistanceMinM, p.DistanceMaxM, u);
            double startAngle = 2.0 * System.Math.PI * v;
            double step = p.StepDegrees * (System.Math.PI / 180.0);
            for (int k = 0; k < p.Tries; k++)
            {
                double angle = startAngle + k * step;
                var candidate = anchorM + distance * new double3(math.cos(angle), 0, math.sin(angle));
                if (math.length((candidate - playerM).xz) < p.MinPlayerDistanceM) continue;
                bool clear = true;
                for (int i = 0; i < content.Bodies.Length; i++)
                {
                    var body = content.Bodies[i];
                    if ((body.State & BodyStateFlags.Destroyed) != 0) continue;
                    if (body.Id.Role == BodyRole.Belt)
                    {
                        double radius = math.length(candidate.xz);
                        if (radius < (double)body.BeltInnerRadiusM - p.BodyClearanceM ||
                            radius > (double)body.BeltOuterRadiusM + p.BodyClearanceM) continue;
                    }
                    else if (math.length((candidate - OrbitMath.Position(body, universalTime)).xz) >=
                        (double)body.RadiusM + p.BodyClearanceM) continue;
                    clear = false;
                    break;
                }
                if (!clear) continue;
                centreM = candidate;
                return true;
            }
            return false;
        }

        public static void ShipPoses(double3 centreM, double3 facePointM, IReadOnlyList<PlannedShip> ships, float spacingM,
            FormationDefinition formation, List<(double3 pos, float yaw)> into)
        {
            into.Clear();
            var forward = (facePointM - centreM).xz;
            double length = math.length(forward);
            forward = length > 0 ? forward / length : new double2(0, 1);
            float yaw = (float)math.atan2(forward.x, forward.y);
            if (formation != null && formation.Slots.Count >= ships.Count - 1)
            {
                var rotation = quaternion.RotateY(yaw);
                for (int k = 0; k < ships.Count; k++)
                {
                    var offset = k == 0 ? float3.zero :
                        new float3(formation.Slots[k - 1].x, 0, formation.Slots[k - 1].y);
                    into.Add((centreM + (double3)math.rotate(rotation, offset), yaw));
                }
                return;
            }
            var right = new double3(forward.y, 0, -forward.x);
            float largestRadius = 0;
            for (int i = 0; i < ships.Count; i++) largestRadius = math.max(largestRadius, ships[i].RadiusM);
            float spacing = spacingM > 0 ? spacingM : 2f * largestRadius + 20f;
            for (int k = 0; k < ships.Count; k++)
                into.Add((centreM + right * ((k - (ships.Count - 1) / 2.0) * spacing), yaw));
        }
    }
}
