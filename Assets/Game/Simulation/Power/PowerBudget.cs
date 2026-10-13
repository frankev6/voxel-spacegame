namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.9. Authored full-demand totals; no simulation state is written.</summary>
    internal readonly struct PowerBudget
    {
        public readonly double Generation, Capacity, WeaponDraw, ShieldDraw, ThrusterDraw;

        public PowerBudget(double generation, double capacity, double weaponDraw, double shieldDraw, double thrusterDraw)
        {
            Generation = generation; Capacity = capacity; WeaponDraw = weaponDraw;
            ShieldDraw = shieldDraw; ThrusterDraw = thrusterDraw;
        }

        public double SustainSeconds
        {
            get
            {
                double deficit = WeaponDraw + ShieldDraw + ThrusterDraw - Generation;
                return deficit > 0 ? Capacity / deficit : double.PositiveInfinity;
            }
        }

        // No surplus means storage cannot recharge, including on a coreless ship.
        public double RechargeSeconds => Generation > ShieldDraw
            ? Capacity / (Generation - ShieldDraw) : double.PositiveInfinity;
    }
}
