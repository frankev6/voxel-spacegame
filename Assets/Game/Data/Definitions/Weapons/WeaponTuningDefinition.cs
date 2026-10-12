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

        // TODO(WPN-002): WPN-E007 validation.
        public override void Validate(ValidationScope scope, ContentValidationContext context)
            => throw new System.NotImplementedException();
    }
}
