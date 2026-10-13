using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [ContentIdPrefix("body_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Body Template", fileName = "body_new")]
    public sealed class BodyTemplateDefinition : ContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField, UnityEngine.TextArea(2, 5)] private string description;
        [SerializeField] private BodyRole role;
        [SerializeField] private float radiusMinM = 100f, radiusMaxM = 100f;  // visual/collider radius (celestials) or bounding radius before a grid exists
        [SerializeField] private BodyVisualDefinition visual;
        [SerializeField] private StarClass starClass;                          // Star only
        [SerializeField] private bool hasCollider = true;                      // Star/Planet/Moon: static sphere collider
        [SerializeField] private bool canHaveMoons;                            // Planet only
        [SerializeField] private InteractionKind interaction;
        [SerializeField] private float interactionDistanceM = 150f;           // measured to the collider surface (SPEC-31 R10)
        [SerializeField] private string interactPrompt;                        // e.g. "Dock"; "" = none
        [SerializeField] private string storyTag;                              // optional narrative location tag, snake_case
        [SerializeField] private StructureSource structureSource;
        [SerializeField] private ShipBlueprintDefinition blueprint;             // structureSource == Blueprint
        [SerializeField] private ProceduralShapeParams shape;                   // structureSource == ProceduralShape
        [SerializeField] private bool destructible = true;                     // false: SPEC-13 DamageImmune (§10 Q15)
        [SerializeField] private float t2LoadDistanceM = 6000f;                // SPEC-31 tier T2, measured to the bounding sphere
        // Belt only
        [SerializeField] private float beltWidthM = 2000f;
        [SerializeField] private float sectorSizeM = 500f;
        [SerializeField] private int asteroidsPerSectorMin = 2, asteroidsPerSectorMax = 6;   // max ≤ 8
        [SerializeField] private float asteroidRadiusMinM = 4f, asteroidRadiusMaxM = 18f;
        [SerializeField] private BlockDefinition asteroidBlock;
        [SerializeField] private float asteroidNoiseAmplitude = 0.25f, asteroidNoiseFrequency = 0.15f;

        public string DisplayName => displayName;
        public string Description => description;
        public BodyRole Role => role;
        public float RadiusMinM => radiusMinM;
        public float RadiusMaxM => radiusMaxM;
        public BodyVisualDefinition Visual => visual;
        public StarClass StarClass => starClass;
        public bool HasCollider => hasCollider;
        public bool CanHaveMoons => canHaveMoons;
        public InteractionKind Interaction => interaction;
        public float InteractionDistanceM => interactionDistanceM;
        public string InteractPrompt => interactPrompt;
        public string StoryTag => storyTag;
        public StructureSource StructureSource => structureSource;
        public ShipBlueprintDefinition Blueprint => blueprint;
        public ProceduralShapeParams Shape => shape;
        public bool Destructible => destructible;
        public float T2LoadDistanceM => t2LoadDistanceM;
        public float BeltWidthM => beltWidthM;
        public float SectorSizeM => sectorSizeM;
        public int AsteroidsPerSectorMin => asteroidsPerSectorMin;
        public int AsteroidsPerSectorMax => asteroidsPerSectorMax;
        public float AsteroidRadiusMinM => asteroidRadiusMinM;
        public float AsteroidRadiusMaxM => asteroidRadiusMaxM;
        public BlockDefinition AsteroidBlock => asteroidBlock;
        public float AsteroidNoiseAmplitude => asteroidNoiseAmplitude;
        public float AsteroidNoiseFrequency => asteroidNoiseFrequency;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            WorldDefinitionValidation.Range(scope, nameof(radiusMinM), radiusMinM, radiusMaxM);
            if (structureSource == Game.Data.Definitions.StructureSource.Blueprint)
                WorldDefinitionValidation.Require(scope, "GAL-E015", nameof(blueprint), blueprint != null, "Blueprint structure source requires a blueprint.");
            if (structureSource == Game.Data.Definitions.StructureSource.ProceduralShape)
            {
                WorldDefinitionValidation.Require(scope, "GAL-E015", "shape.sizeCells", shape.sizeCells.x > 0 && shape.sizeCells.y > 0 && shape.sizeCells.z > 0, "Shape dimensions must be positive.");
                WorldDefinitionValidation.Require(scope, "GAL-E015", "shape.wallThicknessCells", shape.wallThicknessCells >= 0, "Wall thickness must be nonnegative; zero is solid.");
                WorldDefinitionValidation.Require(scope, "GAL-E015", "shape.hullBlock", shape.hullBlock != null, "Shape needs a hull block.");
                WorldDefinitionValidation.Require(scope, "GAL-E015", "shape.coreBlock", !destructible || shape.coreBlock != null, "Destructible shape needs a core block.");
            }
            if (role == BodyRole.Belt)
            {
                WorldDefinitionValidation.Require(scope, "GAL-E016", nameof(asteroidBlock), asteroidBlock != null, "Belt requires an asteroid block.");
                WorldDefinitionValidation.Require(scope, "GAL-E016", nameof(asteroidsPerSectorMin), asteroidsPerSectorMin >= 1 && asteroidsPerSectorMax >= asteroidsPerSectorMin && asteroidsPerSectorMax <= 8, "Asteroid counts must be ordered in 1..8.");
                WorldDefinitionValidation.Require(scope, "GAL-E016", nameof(sectorSizeM), float.IsFinite(sectorSizeM) && sectorSizeM >= 100, "Sector size must be finite and at least 100 metres.");
                WorldDefinitionValidation.Range(scope, nameof(asteroidRadiusMinM), asteroidRadiusMinM, asteroidRadiusMaxM);
                WorldDefinitionValidation.Positive(scope, nameof(beltWidthM), beltWidthM);
            }
        }
    }
}
