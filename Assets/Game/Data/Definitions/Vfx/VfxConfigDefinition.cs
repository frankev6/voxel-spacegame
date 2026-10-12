using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-63 §4; shared content boundary per ADR-0050.</summary>
    [ContentIdPrefix("config_")]
    [CreateAssetMenu(menuName = "Game/Content/VFX Config", fileName = "config_vfx")]
    public sealed class VfxConfigDefinition : ContentDefinition
    {
        [SerializeField] FxDefinition[] defaults = new FxDefinition[14]; // index = (int)FxKind; [0] stays null
        [SerializeField] FxQualityBudget low    = new FxQualityBudget { DebrisLiveCap = 10000, DebrisSpawnPerFrame = 4000,  ParticleFactor = 0.25f, MaxTracers = 4096,  MaxBeams = 64,  MaxPlumes = 256,  MaxShields = 32 };
        [SerializeField] FxQualityBudget medium = new FxQualityBudget { DebrisLiveCap = 25000, DebrisSpawnPerFrame = 10000, ParticleFactor = 0.5f,  MaxTracers = 8192,  MaxBeams = 128, MaxPlumes = 512,  MaxShields = 64 };
        [SerializeField] FxQualityBudget high   = new FxQualityBudget { DebrisLiveCap = 50000, DebrisSpawnPerFrame = 20000, ParticleFactor = 1f,    MaxTracers = 16384, MaxBeams = 256, MaxPlumes = 1024, MaxShields = 128 };
        [SerializeField] Color32[] fallbackCellPalette = new Color32[256]; // used when GameRoot passes no palette
        [SerializeField] float debrisLifetimeMin = 3f, debrisLifetimeMax = 6f;
        [SerializeField] float debrisFullDetailDistance = 150f;   // beyond: keep 1 debris event in 4, size × 1.6
        [SerializeField] float tracerLengthSeconds = 0.02f, tracerMinLength = 0.5f, tracerMaxLength = 12f, tracerWidth = 0.15f;
        [SerializeField] Color[] tracerColorByDamageType = { new Color(1f, .85f, .4f), new Color(.4f, .9f, 1f), new Color(1f, .5f, .2f), Color.white };
        [SerializeField] float beamDefaultWidth = 0.4f;
        [SerializeField] float plumeLengthAtFullIntensity = 6f, plumeMinIntensity = 0.02f;
        [SerializeField] float shieldRippleSeconds = 0.6f, shieldHitFlashSeconds = 0.15f;
        [SerializeField] float chunkDissolveSeconds = 2f;
        public FxDefinition[] Defaults => defaults;
        public FxQualityBudget Low => low;
        public FxQualityBudget Medium => medium;
        public FxQualityBudget High => high;
        public Color32[] FallbackCellPalette => fallbackCellPalette;
        public float DebrisLifetimeMin => debrisLifetimeMin;
        public float DebrisLifetimeMax => debrisLifetimeMax;
        public float DebrisFullDetailDistance => debrisFullDetailDistance;
        public float TracerLengthSeconds => tracerLengthSeconds;
        public float TracerMinLength => tracerMinLength;
        public float TracerMaxLength => tracerMaxLength;
        public float TracerWidth => tracerWidth;
        public Color[] TracerColorByDamageType => tracerColorByDamageType;
        public float BeamDefaultWidth => beamDefaultWidth;
        public float PlumeLengthAtFullIntensity => plumeLengthAtFullIntensity;
        public float PlumeMinIntensity => plumeMinIntensity;
        public float ShieldRippleSeconds => shieldRippleSeconds;
        public float ShieldHitFlashSeconds => shieldHitFlashSeconds;
        public float ChunkDissolveSeconds => chunkDissolveSeconds;
        public FxQualityBudget Budget(VfxQuality q) => q == VfxQuality.Low ? low : q == VfxQuality.Medium ? medium : high;
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (defaults == null || defaults.Length != 14) scope.Error("VFX-E010", nameof(defaults), "Defaults must have 14 entries indexed by FxKind.");
            else for (int k = 1; k < defaults.Length; k++)
            {
                if (defaults[k] == null) scope.Error("VFX-E011", $"defaults[{k}]", "A default is required for each effect kind.");
                else if (defaults[k].Kind != (FxKind)k) scope.Error("VFX-E012", $"defaults[{k}]", "Default kind must match its array index.");
            }
            if (fallbackCellPalette == null || fallbackCellPalette.Length != 256)
                scope.Error("VFX-E013", nameof(fallbackCellPalette), "Fallback palette must contain 256 entries.");
            if (debrisLifetimeMin > debrisLifetimeMax)
                scope.Error("VFX-E014", nameof(debrisLifetimeMin), "Minimum debris lifetime must not exceed maximum.");
            if (tracerColorByDamageType == null || tracerColorByDamageType.Length != 4)
                scope.Error("VFX-E015", nameof(tracerColorByDamageType), "Tracer colours must contain all four damage types.");
        }
    }
}
