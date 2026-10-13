using System;
using Game.Data.Definitions;
using Game.Galaxy.Data;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §5.1: pure deterministic lane cost, with float-product epsilon.</summary>
    public static class FuelMath
    {
        public static int Cost(in LaneDef lane, float fuelPerLy, int fuelMinPerJump, int ancientGateFuel)
        {
            if (lane.Kind == LaneKind.AncientGate) return ancientGateFuel;
            double rounded = Math.Ceiling((double)lane.LengthLy * fuelPerLy - 1e-6);
            return Math.Max(fuelMinPerJump, rounded >= int.MaxValue ? int.MaxValue : (int)rounded);
        }
    }
}
