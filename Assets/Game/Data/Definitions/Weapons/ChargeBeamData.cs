namespace Game.Data.Definitions
{
    /// <summary>SPEC-21 §4.6d (WPN-019): the charge beam's own fields. A trigger starts a charge of <see cref="ChargeSeconds"/>:
    /// <see cref="ConvergeBeams"/> thin beams aim at a ring of <see cref="ConvergeRadius"/> around the aim point and close onto it,
    /// marking the target. Then the main beam (WeaponDefinition.beamWidth) fires for beamPulseSeconds. Throughout, the beam turns
    /// toward the aim at no more than <see cref="TrackRateDeg"/>, so it lags a fast-moving cursor.</summary>
    [System.Serializable]
    public sealed class ChargeBeamData : WeaponDeliveryData
    {
        [UnityEngine.SerializeField, UnityEngine.Min(1f / 60f)] private float chargeSeconds = 1.5f;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float trackRateDeg = 30;
        [UnityEngine.SerializeField, UnityEngine.Range(2, 12)] private int convergeBeams = 6;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float convergeRadius = 8;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float convergeBeamWidth = 0.15f;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 0.9f)] private float convergedHoldFraction = 0.2f;

        public override WeaponDelivery Delivery => WeaponDelivery.ChargeBeam;
        /// <summary>s from the trigger until the main beam fires.</summary>
        public float ChargeSeconds => chargeSeconds;
        /// <summary>Most degrees per second the beam turns toward the aim, while charging and firing.</summary>
        public float TrackRateDeg => trackRateDeg;
        /// <summary>Thin beams drawn while charging.</summary>
        public int ConvergeBeams => convergeBeams;
        /// <summary>m: radius of the ring around the aim point the thin beams start on.</summary>
        public float ConvergeRadius => convergeRadius;
        /// <summary>m: width of each thin charging beam (the main beam is WeaponDefinition.beamWidth, wider).</summary>
        public float ConvergeBeamWidth => convergeBeamWidth;
        /// <summary>Last fraction of the charge the thin beams spend merged into one, before the main beam fires.</summary>
        public float ConvergedHoldFraction => convergedHoldFraction;

        public ChargeBeamData() { }
        public ChargeBeamData(float chargeSeconds, float trackRateDeg, int convergeBeams, float convergeRadius, float convergeBeamWidth, float convergedHoldFraction)
        {
            this.chargeSeconds = chargeSeconds; this.trackRateDeg = trackRateDeg; this.convergeBeams = convergeBeams;
            this.convergeRadius = convergeRadius; this.convergeBeamWidth = convergeBeamWidth; this.convergedHoldFraction = convergedHoldFraction;
        }
        static bool Finite(float value, float min) => float.IsFinite(value) && value >= min;
        /// <summary>WPN-E003 checks of these fields, reported against <c>deliveryData</c>.</summary>
        public void Validate(Game.Data.Validation.ValidationScope scope, string field)
        {
            if (!Finite(chargeSeconds, Game.Core.SimTime.FixedTimestep)) scope.Error("WPN-E003", field, "Charge must last at least one fixed tick.");
            if (!Finite(trackRateDeg, 0)) scope.Error("WPN-E003", field, "Track rate must be finite and nonnegative.");
            if (convergeBeams < 2 || convergeBeams > 12) scope.Error("WPN-E003", field, "Converging beams must be 2..12.");
            if (!Finite(convergeRadius, 0)) scope.Error("WPN-E003", field, "Converge radius must be finite and nonnegative.");
            if (!Finite(convergeBeamWidth, 0)) scope.Error("WPN-E003", field, "Converging beam width must be finite and nonnegative.");
            if (!float.IsFinite(convergedHoldFraction) || convergedHoldFraction < 0 || convergedHoldFraction > 0.9f) scope.Error("WPN-E003", field, "Converged hold must be 0..0.9 of the charge.");
        }
    }
}
