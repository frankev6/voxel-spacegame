namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct StatTotals
    {
        public double MassKg;
        public int Blocks;
        public int Cells;
        public double ForwardThrustN; // along grid +Z
        public double PowerGen, PowerUse, PowerStorage, Dps, ShieldCapacity, ShieldRegen, CargoUnits;
        public long Cost;             // Σ count × ItemDefinition.BasePrice
        public Unity.Mathematics.double3 CenterOfMassCells; // grid cell units
        public double ThrustToWeight => MassKg > 0 ? ForwardThrustN / (MassKg * 9.81) : 0;
    }
}
