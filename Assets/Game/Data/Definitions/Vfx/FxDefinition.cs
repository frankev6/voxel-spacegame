using Game.Data.Validation;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-63 §4; shared content boundary per ADR-0050.</summary>
    [ContentIdPrefix("fx_")]
    [CreateAssetMenu(menuName = "Game/Content/FX", fileName = "fx_new")]
    public sealed class FxDefinition : ContentDefinition
    {
        [SerializeField] FxKind kind;
        [SerializeField] FxMode mode = FxMode.Batched;
        [SerializeField] VisualEffectAsset graph;              // Batched/Pooled; null for Instanced
        [SerializeField] Mesh mesh;                            // Instanced only (built-in Quad or Sphere for templates)
        [SerializeField] Material material;                    // Instanced only
        [SerializeField] Color defaultColor = Color.white;     // used when FxEvent.Color == 0
        [SerializeField, Min(0.01f)] float scale = 1f;         // multiplier on FxEvent.Scale
        [SerializeField, Range(1, 64)] int particlesPerEvent = 8;      // Batched, at High quality; Debris must be 1
        [SerializeField, Range(1, 65536)] int maxEventsPerFrame = 2048;// Batched/Pooled: GraphicsBuffer capacity
        [SerializeField, Range(1, 16)] int poolSize = 4;               // Pooled
        [SerializeField, Min(0.1f)] float maxLifetime = 2f;            // seconds; live-count estimate and anchor reset
        [SerializeField, Min(1f)] float cullDistance = 1500f;          // metres from the reference point (§3.13)
        [SerializeField, Min(1f)] float lodDistance = 300f;            // beyond: particle count × lodParticleFactor, size × 1.6
        [SerializeField, Range(0.05f, 1f)] float lodParticleFactor = 0.25f;
        [SerializeField] VfxQuality minQuality = VfxQuality.Low;       // effect skipped below this level

        public FxKind Kind => kind; public FxMode Mode => mode; public VisualEffectAsset Graph => graph;
        public Mesh Mesh => mesh; public Material Material => material; public Color DefaultColor => defaultColor;
        public float Scale => scale; public int ParticlesPerEvent => particlesPerEvent; public int MaxEventsPerFrame => maxEventsPerFrame;
        public int PoolSize => poolSize; public float MaxLifetime => maxLifetime; public float CullDistance => cullDistance;
        public float LodDistance => lodDistance; public float LodParticleFactor => lodParticleFactor; public VfxQuality MinQuality => minQuality;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            bool instancedKind = kind == FxKind.Beam || kind == FxKind.Thruster || kind == FxKind.Tracer || kind == FxKind.ShieldShell;
            if (kind == FxKind.None) scope.Error("VFX-E001", nameof(kind), "An effect must name a kind.");
            if ((mode == FxMode.Batched || mode == FxMode.Pooled) && graph == null)
                scope.Error("VFX-E002", nameof(graph), "Batched and pooled effects require a graph.");
            if (mode == FxMode.Instanced)
            {
                if (mesh == null) scope.Error("VFX-E003", nameof(mesh), "Instanced effects require a mesh.");
                if (material == null) scope.Error("VFX-E003", nameof(material), "Instanced effects require a material.");
            }
            if ((mode == FxMode.Instanced) != instancedKind)
                scope.Error("VFX-E004", nameof(mode), "Only Beam, Thruster, Tracer and ShieldShell use instanced mode.");
            if (kind == FxKind.Debris && (particlesPerEvent != 1 || mode != FxMode.Batched))
                scope.Error("VFX-E005", nameof(particlesPerEvent), "Debris must be batched with one particle per event.");
            if ((kind == FxKind.ExplosionLarge || kind == FxKind.Warp) && mode != FxMode.Pooled)
                scope.Error("VFX-E006", nameof(mode), "Large explosions and warp effects must be pooled.");
        }
    }
}
