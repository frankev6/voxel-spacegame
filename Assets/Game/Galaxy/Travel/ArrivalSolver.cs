using System;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.6 analytic jump/cruise arrival contracts; no physics queries.</summary>
    public static class ArrivalSolver
    {
        public static ArrivalPlan SolveJump(StarSystemContent content, int laneIndex, double2 targetPositionLy, double2 sourcePositionLy,
            double universalTime, in ArrivalParams p)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            ulong gateKey = 0;
            float gateRadius = 0;
            var direction = Direction(sourcePositionLy - targetPositionLy);
            var gatePosition = new double3(direction.x, 0, direction.y) * content.LaneRingRadiusM;
            if (laneIndex >= 0)
                for (int i = 0; i < content.Bodies.Length; i++)
                {
                    var body = content.Bodies[i];
                    if (body.Id.Role != BodyRole.LaneGate || body.LaneIndex != laneIndex) continue;
                    gatePosition = OrbitMath.Position(body, universalTime);
                    gateRadius = body.RadiusM;
                    gateKey = body.Id.LocalKey;
                    break;
                }
            var outward = Direction(gatePosition.xz);
            var initial = gatePosition - new double3(outward.x, 0, outward.y) * (gateRadius + (double)p.StandoffM);
            var plan = Search(content, initial, double3.zero, universalTime, p);
            plan.GateKey = gateKey;
            plan.GatePositionM = gatePosition;
            plan.GateRadiusM = gateRadius;
            return plan;
        }

        public static ArrivalPlan SolveCruise(StarSystemContent content, double3 fromM, double3 targetM, float targetRadiusM,
            double universalTime, in ArrivalParams p)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var direction = Direction((targetM - fromM).xz);
            var initial = targetM - new double3(direction.x, 0, direction.y) * (targetRadiusM + (double)p.StandoffM);
            var plan = Search(content, initial, targetM, universalTime, p);
            plan.GatePositionM = targetM;
            plan.GateRadiusM = targetRadiusM;
            return plan;
        }

        static ArrivalPlan Search(StarSystemContent content, double3 initial, double3 centre, double time, in ArrivalParams p)
        {
            var plan = new ArrivalPlan { StarSystem = content.Id, PositionM = initial };
            if (Obstructed(content, initial, time, p.ClearanceM))
            {
                plan.Obstructed = 1;
                double2 offset = (initial - centre).xz;
                double radius = math.length(offset);
                double angle = math.atan2(offset.y, offset.x);
                // A zero-radius ring has only the initial point; angular steps cannot find another point.
                if (radius > 0)
                    for (int k = 1; k <= p.MaxSteps; k++)
                    {
                        int step = (k + 1) / 2 * ((k & 1) != 0 ? 1 : -1);
                        double theta = angle + step * (double)p.StepM / radius;
                        var position = centre + new double3(radius * math.cos(theta), 0, radius * math.sin(theta));
                        if (Obstructed(content, position, time, p.ClearanceM)) continue;
                        plan.PositionM = position;
                        plan.Adjusted = 1;
                        plan.Obstructed = 0;
                        break;
                    }
            }
            var facing = Direction((centre - plan.PositionM).xz);
            plan.YawRad = (float)math.atan2(facing.x, facing.y);
            return plan;
        }

        static bool Obstructed(StarSystemContent content, double3 position, double time, float clearance)
        {
            double radius = math.length(position.xz);
            for (int i = 0; i < content.Bodies.Length; i++)
            {
                var body = content.Bodies[i];
                if ((body.State & BodyStateFlags.Destroyed) != 0) continue;
                if (body.Id.Role == BodyRole.Belt)
                {
                    if (body.BeltInnerRadiusM - (double)clearance < radius && radius < body.BeltOuterRadiusM + (double)clearance) return true;
                }
                else if (math.length((position - OrbitMath.Position(body, time)).xz) < body.RadiusM + (double)clearance) return true;
            }
            return false;
        }

        static double2 Direction(double2 value)
        {
            double length = math.length(value);
            return length > 0 ? value / length : new double2(1, 0);
        }
    }
}
