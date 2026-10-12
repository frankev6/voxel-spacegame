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
            if (maxProjectiles < 1 || maxCandidates < 1 || !float.IsFinite(fixedConvergenceDeg) || fixedConvergenceDeg < 0 || fixedConvergenceDeg > 30 ||
                !float.IsFinite(explosionImpulsePerDamage) || explosionImpulsePerDamage < 0 || !float.IsFinite(maxTargetRange) || maxTargetRange <= 0 ||
                !float.IsFinite(underCursorRadius) || underCursorRadius <= 0)
                scope.Error("WPN-E007", "tuning", "Weapon tuning limits must be positive and finite.");
        }
    }
}
