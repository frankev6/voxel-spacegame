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
            if (!(fireRate > 0) || float.IsInfinity(fireRate) || damage < 0 || !float.IsFinite(damage) || !float.IsFinite(explosionDamage) || explosionDamage < 0)
                scope.Error("WPN-E001", nameof(fireRate), "Rate must be positive and damage finite and nonnegative.");
            if (burstCount < 1 || burstCount > ushort.MaxValue || burstInterval < 0 || !float.IsFinite(burstInterval) || pelletsPerRound < 1 || pelletsPerRound > 32)
                scope.Error("WPN-E002", nameof(burstCount), "Invalid burst or pellet count.");
            if (delivery != WeaponDelivery.Beam && (!(projectileSpeed > 0) || !float.IsFinite(projectileSpeed) || !(lifetimeMin > 0) || lifetimeMax < lifetimeMin || !float.IsFinite(lifetimeMax)))
                scope.Error("WPN-E003", nameof(projectileSpeed), "Projectiles need finite positive speed and ordered positive lifetimes.");
            if (damageType == Game.Data.Combat.DamageType.Ram) scope.Error("WPN-E008", nameof(damageType), "Ram is not a weapon damage type.");
            // TODO(WPN-002): complete validation for all delivery variants and cross-content references.
        }
    }
}
