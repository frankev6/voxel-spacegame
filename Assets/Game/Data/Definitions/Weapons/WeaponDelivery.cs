namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponDelivery. Add values at the end only.</summary>
    public enum WeaponDelivery : byte { Projectile = 0, Beam = 1, Missile = 2, ChargeBeam = 3 }

    /// <summary>SPEC-21 §4.6d (WPN-019): which deliveries are beams (a hit-scan cast with BeamRange, damage spread over BeamPulseSeconds).</summary>
    public static class WeaponDeliveryKinds
    {
        public static bool IsBeam(WeaponDelivery delivery) => delivery == WeaponDelivery.Beam || delivery == WeaponDelivery.ChargeBeam;
    }
}
