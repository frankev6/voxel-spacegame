using Game.Data.Combat;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-13 section 4.7: one content row per damage type; Game.App installs its table at boot.</summary>
    [ContentIdPrefix("dmg_")]
    [CreateAssetMenu(menuName = "Game/Content/Damage Type", fileName = "dmg_")]
    public sealed class DamageTypeDefinition : ContentDefinition
    {
        [SerializeField] private DamageType type;
        [SerializeField] private float[] armorMultipliers = System.Array.Empty<float>();
        [SerializeField, Min(0)] private float kickMin;
        [SerializeField, Min(0)] private float kickMax;
        [SerializeField, Min(0)] private float shieldMultiplier = 1;
        [SerializeField] private bool bypassShields;
        public DamageType Type => type;
        public System.Collections.Generic.IReadOnlyList<float> ArmorMultipliers => armorMultipliers;
        public float KickMin => kickMin;
        public float KickMax => kickMax;
        public float ShieldMultiplier => shieldMultiplier;
        public bool BypassShields => bypassShields;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (armorMultipliers == null || armorMultipliers.Length == 0)
                scope.Error("DMG-E001", nameof(armorMultipliers), "One multiplier per armor class is required.");
            else
                foreach (float value in armorMultipliers)
                    if (!Nonnegative(value)) { scope.Error("DMG-E001", nameof(armorMultipliers), "Multipliers must be finite and nonnegative."); break; }
            if (!Nonnegative(shieldMultiplier)) scope.Error("DMG-E001", nameof(shieldMultiplier), "Must be finite and nonnegative.");
            if (!Nonnegative(kickMin)) scope.Error("DMG-E003", nameof(kickMin), "Must be finite and nonnegative.");
            if (!Nonnegative(kickMax)) scope.Error("DMG-E003", nameof(kickMax), "Must be finite and nonnegative.");
            if (kickMin > kickMax) scope.Error("DMG-E003", nameof(kickMin), "Must not exceed kickMax.");
            if (context?.Catalog == null) return;
            int count = context.Catalog.All<ArmorClassDefinition>().Count;
            if (armorMultipliers != null && armorMultipliers.Length != count)
                scope.Error("DMG-E001", nameof(armorMultipliers), "Length must equal the number of armor class definitions.");
            var types = context.Catalog.All<DamageTypeDefinition>();
            if (types.Count < System.Enum.GetValues(typeof(DamageType)).Length)
                scope.Error("DMG-E002", nameof(type), "Every declared damage type needs a definition.");
            if ((byte)type >= types.Count) scope.Error("DMG-E002", nameof(type), "Damage type indices must be contiguous from zero.");
            foreach (var other in types)
                if (other != this && other.type == type) { scope.Error("DMG-E002", nameof(type), "Duplicate damage type."); break; }
        }
        static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    }
}
