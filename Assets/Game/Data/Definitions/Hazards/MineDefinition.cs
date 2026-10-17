using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>A mine: the device blueprint carrying a ProximityFuse block, and its anchor defaults (SPEC-91 §4.4).</summary>
    [ContentIdPrefix("mine_")]
    [CreateAssetMenu(menuName = "Game/Content/Hazards/Mine", fileName = "mine_new")]
    public sealed class MineDefinition : ContentDefinition
    {
        [SerializeField] private ShipBlueprintDefinition blueprint;
        [SerializeField] private bool anchored = true;
        [SerializeField, Min(0)] private float anchorStiffness = 0.4f;    // 1/s²
        [SerializeField, Min(0)] private float anchorDamping = 0.8f;      // 1/s
        [SerializeField, Min(0)] private float anchorMaxAccel = 6f;       // m/s²
        [SerializeField, Min(0)] private float radarRange = 300f;         // m; 0 = never on radar
        // The fuse, on the mine definition so spawning needs no block decode (HAZ-008; the blueprint is the mine's body).
        [SerializeField] private Game.Data.Blocks.ProximityFuseParams fuse = new Game.Data.Blocks.ProximityFuseParams
        { ArmSeconds = 1.5f, TriggerRadius = 16, FuseSeconds = .6f, BlastRadius = 22, BlastDamage = 900, BlastType = Game.Data.Combat.DamageType.Explosive, MinTriggerMass = 2000 };

        public ShipBlueprintDefinition Blueprint => blueprint;
        public bool Anchored => anchored;
        public float AnchorStiffness => anchorStiffness;
        public float AnchorDamping => anchorDamping;
        public float AnchorMaxAccel => anchorMaxAccel;
        public float RadarRange => radarRange;
        public Game.Data.Blocks.ProximityFuseParams Fuse => fuse;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (blueprint == null) scope.Error("HAZ-E002", nameof(blueprint), "A mine needs a device blueprint.");
        }
    }
}
