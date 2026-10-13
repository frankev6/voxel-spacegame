using UnityEngine;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Authored defaults for SPEC-21 §4.7; factory converts degrees to radians.</summary>
    [ContentIdPrefix("config_")]
    [CreateAssetMenu(menuName = "Game/Content/Weapon Tuning", fileName = "config_weapons")]
    public sealed class WeaponTuningDefinition : ContentDefinition
    {
        [SerializeField] private int maxProjectiles = 8192;
        [SerializeField] private float fixedConvergenceDeg = 3f;
        [SerializeField] private float explosionImpulsePerDamage = 2f;
        [SerializeField] private float maxTargetRange = 2500f;
        [SerializeField] private float underCursorRadius = 30f;
        [SerializeField] private int maxCandidates = 32;

        public int MaxProjectiles => maxProjectiles;
        public float FixedConvergenceDeg => fixedConvergenceDeg;
        public float ExplosionImpulsePerDamage => explosionImpulsePerDamage;
        public float MaxTargetRange => maxTargetRange;
        public float UnderCursorRadius => underCursorRadius;
        public int MaxCandidates => maxCandidates;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            Check(scope, nameof(maxProjectiles), maxProjectiles >= 256, "Projectile capacity must be at least 256.");
            Check(scope, nameof(maxCandidates), maxCandidates >= 4, "Candidate capacity must be at least four.");
            Check(scope, nameof(fixedConvergenceDeg), float.IsFinite(fixedConvergenceDeg) && fixedConvergenceDeg >= 0 && fixedConvergenceDeg <= 30, "Fixed convergence must be in 0..30 degrees.");
            Check(scope, nameof(explosionImpulsePerDamage), float.IsFinite(explosionImpulsePerDamage) && explosionImpulsePerDamage >= 0, "Explosion impulse must be finite and nonnegative.");
            Check(scope, nameof(maxTargetRange), float.IsFinite(maxTargetRange) && maxTargetRange > 0, "Target range must be finite and positive.");
            Check(scope, nameof(underCursorRadius), float.IsFinite(underCursorRadius) && underCursorRadius > 0, "Cursor radius must be finite and positive.");
        }
        static void Check(ValidationScope scope, string field, bool valid, string message)
        { if (!valid) scope.Error("WPN-E007", field, message); }
    }
}
