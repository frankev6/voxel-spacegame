using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-23 §4.4: FormationDefinition.</summary>
    [ContentIdPrefix("formation_")]
    public sealed class FormationDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private UnityEngine.Vector2[] slots = System.Array.Empty<UnityEngine.Vector2>();   // leader-local XZ offsets in metres (x right, y forward)

        public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector2> Slots => slots;

        /// <summary>AI-E004: at least one finite follower offset; no pair closer than 20 metres (AI-013).</summary>
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (slots == null || slots.Length == 0)
            {
                scope.Error("AI-E004", nameof(slots), "A formation needs at least one follower slot.");
                return;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                if (!Finite(slots[i]))
                {
                    scope.Error("AI-E004", $"slots[{i}]", "A follower offset must have finite X and forward coordinates.");
                    continue;
                }
                for (int j = 0; j < i; j++)
                {
                    if (!Finite(slots[j])) continue;
                    // Double differences prevent overflow when validating otherwise-finite authored floats.
                    double x = (double)slots[i].x - slots[j].x, z = (double)slots[i].y - slots[j].y;
                    if (x * x + z * z < 20d * 20d)
                        scope.Error("AI-E004", $"slots[{i}]", $"Slots {j} and {i} are closer than 20 metres.");
                }
            }
        }

        static bool Finite(UnityEngine.Vector2 slot) => float.IsFinite(slot.x) && float.IsFinite(slot.y);
    }
}
