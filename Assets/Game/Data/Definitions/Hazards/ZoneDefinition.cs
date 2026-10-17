using Game.Data.Hazards;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>An authored zone: shape, size and effects; placed with a pose by HazardSpawn (SPEC-91 §4.5).</summary>
    [ContentIdPrefix("zone_")]
    [CreateAssetMenu(menuName = "Game/Content/Hazards/Zone", fileName = "zone_new")]
    public sealed class ZoneDefinition : ContentDefinition
    {
        [System.Serializable] public struct Effect
        {
            public ZoneEffectKind Kind;
            public float A, B, C;                    // meaning per kind: SPEC-91 §4.3
            public ushort Id;
            public ZoneEffectFlags Flags;
        }

        [SerializeField] private ZoneShape shape = ZoneShape.Box;
        [SerializeField] private Vector2 halfExtents = new Vector2(60, 100);   // Box: half width, half length; Circle: x = radius
        [SerializeField, Min(0)] private float pathRadius = 8f;              // Path zones
        [SerializeField] private Effect[] effects = { new Effect { Kind = ZoneEffectKind.Accelerate, A = 140, B = 60, Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices } };

        public ZoneShape Shape => shape;
        public Vector2 HalfExtents => halfExtents;
        public float PathRadius => pathRadius;
        public System.Collections.Generic.IReadOnlyList<Effect> Effects => effects;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (effects == null || effects.Length == 0) scope.Error("HAZ-E001", nameof(effects), "A zone needs at least one effect.");
            if (shape == ZoneShape.Path ? !(pathRadius > 0) : !(halfExtents.x > 0) || shape == ZoneShape.Box && !(halfExtents.y > 0))
                scope.Error("HAZ-E001", nameof(halfExtents), "Zone size must be positive.");
        }
    }
}
