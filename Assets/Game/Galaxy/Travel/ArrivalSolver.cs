using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.6 analytic jump/cruise arrival contracts; no physics queries.</summary>
    public static class ArrivalSolver
    {
        public static ArrivalPlan SolveJump(StarSystemContent content, int laneIndex, double2 targetPositionLy, double2 sourcePositionLy,
            double universalTime, in ArrivalParams p) => throw new System.NotImplementedException("TRV-005");
        public static ArrivalPlan SolveCruise(StarSystemContent content, double3 fromM, double3 targetM, float targetRadiusM,
            double universalTime, in ArrivalParams p) => throw new System.NotImplementedException("TRV-013");
    }
}
