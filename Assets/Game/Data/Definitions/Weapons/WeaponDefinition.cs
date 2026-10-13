using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponDefinition.</summary>
    [ContentIdPrefix("wpn_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Weapon", fileName = "wpn_new")]
    public sealed class WeaponDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;
        [UnityEngine.SerializeField] private WeaponDelivery delivery;
        [UnityEngine.SerializeField] private Game.Data.Combat.DamageType damageType;    // Kinetic, Energy or Explosive (Ram is rejected, WPN-E008)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float damage;              // per round (projectile/missile direct hit) or per pulse (beam)
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private float penetrationCells = 1; // point hits: max solid cells destroyed along the ray
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float explosionRadius;     // m; 0 = no explosion
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float explosionDamage;     // raw damage at the blast centre
        [UnityEngine.SerializeField] private bool explodeOnExpire;                          // fuse: explode at end of life (flak, missiles)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float impulsePerHit;       // N·s along the shot direction (knockback)
        [UnityEngine.SerializeField, UnityEngine.Min(0.01f)] private float fireRate = 1;    // triggers per second
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private int burstCount = 1;        // rounds per trigger
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float burstInterval;       // s between rounds of a burst
        [UnityEngine.SerializeField, UnityEngine.Range(1, 32)] private int pelletsPerRound = 1;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 45)] private float spreadDeg;     // cone half-angle per pellet
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float projectileSpeed;     // m/s (projectile, missile)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float projectileRadius = 0.1f; // m: hit-test inflation and tracer width
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float lifetimeMin = 1, lifetimeMax = 1; // s (min = max: fixed)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float homingTurnRateDeg;   // missiles: deg/s; 0 = straight
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float beamRange;           // m (beam)
        [UnityEngine.SerializeField, UnityEngine.Min(1f / 60f)] private float beamPulseSeconds = 0.5f;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float beamWidth = 0.3f;    // m (visual)
        [UnityEngine.SerializeField, UnityEngine.Range(0, 180)] private float traverseHalfArcDeg; // 0 = fixed
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float traverseRateDeg = 180;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 60)] private float maxElevationDeg = 20;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 30)] private float fireConeDeg = 3;   // turrets fire when within this of the desired yaw
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float powerDraw;            // E/s while the group is held
        [UnityEngine.SerializeField] private WeaponFireGroups defaultFireGroups = WeaponFireGroups.Group1;
        [UnityEngine.SerializeField] private ProjectileVisual visual;
        [UnityEngine.SerializeField] private UnityEngine.Color tracerColor = UnityEngine.Color.white;
        [UnityEngine.SerializeField] private AudioCueDefinition fireCue;     // null = silent
        [UnityEngine.SerializeField] private FxDefinition muzzleFx;          // null = FxKind default (SPEC-63 Q8)
        [UnityEngine.SerializeField] private FxDefinition hitFx;             // null = FxKind default
        [UnityEngine.SerializeReference] private WeaponDeliveryData deliveryData;  // §4.6c; null for Projectile/Beam/Missile
        public string DisplayName => displayName;
        public WeaponDelivery Delivery => delivery;
        public Game.Data.Combat.DamageType DamageType => damageType;
        public float Damage => damage;
        public float PenetrationCells => penetrationCells;
        public float ExplosionRadius => explosionRadius;
        public float ExplosionDamage => explosionDamage;
        public bool ExplodeOnExpire => explodeOnExpire;
        public float ImpulsePerHit => impulsePerHit;
        public float FireRate => fireRate;
        public int BurstCount => burstCount;
        public float BurstInterval => burstInterval;
        public int PelletsPerRound => pelletsPerRound;
        public float SpreadDeg => spreadDeg;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRadius => projectileRadius;
        public float LifetimeMin => lifetimeMin;
        public float LifetimeMax => lifetimeMax;
        public float HomingTurnRateDeg => homingTurnRateDeg;
        public float BeamRange => beamRange;
        public float BeamPulseSeconds => beamPulseSeconds;
        public float BeamWidth => beamWidth;
        public float TraverseHalfArcDeg => traverseHalfArcDeg;
        public float TraverseRateDeg => traverseRateDeg;
        public float MaxElevationDeg => maxElevationDeg;
        public float FireConeDeg => fireConeDeg;
        public float PowerDraw => powerDraw;
        public WeaponFireGroups DefaultFireGroups => defaultFireGroups;
        public ProjectileVisual Visual => visual;
        public UnityEngine.Color TracerColor => tracerColor;
        public AudioCueDefinition FireCue => fireCue;
        public FxDefinition MuzzleFx => muzzleFx;
        public FxDefinition HitFx => hitFx;
        public WeaponDeliveryData DeliveryData => deliveryData;
        /// §4.9 formula on the authored values (editor stats through SPEC-04 WeaponMountModule; WeaponContent.ComputeSustainedDps gives the same number).
        public float SustainedDps => (delivery == WeaponDelivery.Beam ? damage : (damage + explosionDamage * .5f) * pelletsPerRound * burstCount) * fireRate;
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            Check(scope, "WPN-E001", nameof(fireRate), fireRate > 0 && float.IsFinite(fireRate) && float.IsFinite(Game.Core.SimTime.TicksPerSecond / fireRate), "Fire rate must produce a finite positive tick interval.");
            Check(scope, "WPN-E001", nameof(burstCount), burstCount >= 1 && burstCount <= ushort.MaxValue, "Burst count must fit the round counter.");
            Check(scope, "WPN-E001", nameof(burstInterval), Nonnegative(burstInterval) && float.IsFinite(burstInterval * Game.Core.SimTime.TicksPerSecond), "Burst interval must be finite and nonnegative in seconds and ticks.");
            Check(scope, "WPN-E001", nameof(pelletsPerRound), pelletsPerRound >= 1 && pelletsPerRound <= 32, "Pellet count must be in 1..32.");
            Check(scope, "WPN-E001", nameof(spreadDeg), InRange(spreadDeg, 0, 45), "Spread must be in 0..45 degrees.");
            Check(scope, "WPN-E001", nameof(traverseHalfArcDeg), InRange(traverseHalfArcDeg, 0, 180), "Traverse half-arc must be in 0..180 degrees.");
            Check(scope, "WPN-E001", nameof(traverseRateDeg), Nonnegative(traverseRateDeg), "Traverse rate must be finite and nonnegative.");
            Check(scope, "WPN-E001", nameof(maxElevationDeg), InRange(maxElevationDeg, 0, 60), "Elevation must be in 0..60 degrees.");
            Check(scope, "WPN-E001", nameof(fireConeDeg), InRange(fireConeDeg, 0, 30), "Fire cone must be in 0..30 degrees.");
            Check(scope, "WPN-E001", nameof(powerDraw), Nonnegative(powerDraw), "Power draw must be finite and nonnegative.");
            Check(scope, "WPN-E001", nameof(defaultFireGroups), ((byte)defaultFireGroups & ~7) == 0, "Only the three authored fire-group bits are supported.");

            bool projectile = delivery == WeaponDelivery.Projectile || delivery == WeaponDelivery.Missile;
            Check(scope, "WPN-E002", nameof(projectileSpeed), Nonnegative(projectileSpeed) && (!projectile || projectileSpeed > 0), "Projectile/missile speed must be finite and positive; unused speed must be nonnegative.");
            Check(scope, "WPN-E002", nameof(projectileRadius), Nonnegative(projectileRadius), "Projectile radius must be finite and nonnegative.");
            Check(scope, "WPN-E002", nameof(homingTurnRateDeg), Nonnegative(homingTurnRateDeg), "Homing turn rate must be finite and nonnegative.");
            Check(scope, "WPN-E002", nameof(visual), System.Enum.IsDefined(typeof(ProjectileVisual), visual), "Unknown projectile visual.");
            if (delivery == WeaponDelivery.Beam)
            {
                Check(scope, "WPN-E003", nameof(beamRange), beamRange > 0 && float.IsFinite(beamRange), "Beam range must be finite and positive.");
                Check(scope, "WPN-E003", nameof(beamPulseSeconds), beamPulseSeconds >= Game.Core.SimTime.FixedTimestep && float.IsFinite(beamPulseSeconds), "A beam pulse must last at least one fixed tick.");
            }
            else
            {
                Check(scope, "WPN-E003", nameof(beamRange), Nonnegative(beamRange), "Unused beam range must be finite and nonnegative.");
                Check(scope, "WPN-E003", nameof(beamPulseSeconds), Nonnegative(beamPulseSeconds), "Unused beam pulse must be finite and nonnegative.");
            }
            Check(scope, "WPN-E003", nameof(beamWidth), Nonnegative(beamWidth), "Beam width must be finite and nonnegative.");
            if (deliveryData != null)
                Check(scope, "WPN-E003", nameof(deliveryData), deliveryData.Delivery == delivery, "Delivery data must match its weapon's delivery discriminator.");
            // New delivery handlers may use shared fields without a data subclass (SPEC-21 §4.6c).
            Check(scope, "WPN-E004", nameof(explosionRadius), InRange(explosionRadius, 0, 6), "Explosion radius must be finite and in 0..6 metres.");
            Check(scope, "WPN-E005", nameof(lifetimeMin), Nonnegative(lifetimeMin) && (!projectile || lifetimeMin > 0), "Projectile/missile minimum life must be positive; unused life must be nonnegative.");
            Check(scope, "WPN-E005", nameof(lifetimeMax), Nonnegative(lifetimeMax) && lifetimeMax >= lifetimeMin, "Maximum life must be finite and no shorter than minimum life.");
            Check(scope, "WPN-E006", nameof(damage), Nonnegative(damage), "Hit damage must be finite and nonnegative.");
            Check(scope, "WPN-E006", nameof(explosionDamage), Nonnegative(explosionDamage), "Blast damage must be finite and nonnegative.");
            Check(scope, "WPN-E006", nameof(damage), damage > 0 || explosionDamage > 0, "A weapon must deal hit or blast damage.");
            Check(scope, "WPN-E006", nameof(penetrationCells), penetrationCells >= 1 && float.IsFinite(penetrationCells), "Penetration must be finite and at least one cell.");
            Check(scope, "WPN-E006", nameof(impulsePerHit), Nonnegative(impulsePerHit), "Hit impulse must be finite and nonnegative.");
            Check(scope, "WPN-E008", nameof(damageType), System.Enum.IsDefined(typeof(Game.Data.Combat.DamageType), damageType) && damageType != Game.Data.Combat.DamageType.Ram, "Weapons require a supported non-Ram damage type.");
            ValidateReference(scope, context, nameof(fireCue), fireCue);
            ValidateReference(scope, context, nameof(muzzleFx), muzzleFx);
            ValidateReference(scope, context, nameof(hitFx), hitFx);
        }
        static bool Nonnegative(float value) => float.IsFinite(value) && value >= 0;
        static bool InRange(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
        static void Check(ValidationScope scope, string code, string field, bool valid, string message)
        { if (!valid) scope.Error(code, field, message); }
        static void ValidateReference(ValidationScope scope, ContentValidationContext context, string field, ContentDefinition reference)
        {
            // Editor reference walking reports DATA-E010 already; runtime validation needs the same registration check.
            if (context != null && !context.IsEditorPass && reference != null && !context.IsInDatabase(reference))
                scope.Error("DATA-E010", field, "Referenced definition is not in the content database.");
        }
    }
}
