using Game.Galaxy.Data;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.6 fuel-cost contract; TRV-003 supplies the pure formula.</summary>
    public static class FuelMath
    {
        public static int Cost(in LaneDef lane, float fuelPerLy, int fuelMinPerJump, int ancientGateFuel)
            => throw new System.NotImplementedException("TRV-003");
    }
}
