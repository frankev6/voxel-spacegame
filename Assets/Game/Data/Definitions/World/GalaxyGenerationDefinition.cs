using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [ContentIdPrefix("galaxy_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Galaxy Generation", fileName = "galaxy_new")]
    public sealed class GalaxyGenerationDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private int generatorVersion = 1;            // must equal GalaxyConstants.CurrentGeneratorVersion
        [SerializeField] private bool useFixedSeed = true;            // M3: every new game uses fixedSeed
        [SerializeField] private ulong fixedSeed = 0x5EED0001ul;
        [SerializeField] private float galaxyRadiusLy = 60f;
        [SerializeField] private int proceduralStarSystemCount = 0;   // M3 content: 0. Range 0..100000
        [SerializeField] private float minStarSystemSpacingLy = 3f;
        [SerializeField] private int placementAttemptsPerStarSystem = 30;
        [SerializeField] private int laneCandidateNeighbours = 6;
        [SerializeField] private int maxLaneDegree = 4;
        [SerializeField] private float maxLaneLengthLy = 12f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float extraLaneChance = 0.35f;
        [SerializeField] private System.Collections.Generic.List<WeightedStarSystemTemplate> templatePool = new();
        [SerializeField] private System.Collections.Generic.List<AuthoredStarSystemDefinition> authoredStarSystems = new();
        [SerializeField] private AuthoredStarSystemDefinition startStarSystem;
        [SerializeField] private System.Collections.Generic.List<AuthoredLane> authoredLanes = new();
        [SerializeField] private BodyTemplateDefinition defaultHyperlaneGate;
        [SerializeField] private BodyTemplateDefinition defaultAncientGate;

        public int GeneratorVersion => generatorVersion;
        public bool UseFixedSeed => useFixedSeed;
        public ulong FixedSeed => fixedSeed;
        public float GalaxyRadiusLy => galaxyRadiusLy;
        public int ProceduralStarSystemCount => proceduralStarSystemCount;
        public float MinStarSystemSpacingLy => minStarSystemSpacingLy;
        public int PlacementAttemptsPerStarSystem => placementAttemptsPerStarSystem;
        public int LaneCandidateNeighbours => laneCandidateNeighbours;
        public int MaxLaneDegree => maxLaneDegree;
        public float MaxLaneLengthLy => maxLaneLengthLy;
        public float ExtraLaneChance => extraLaneChance;
        public System.Collections.Generic.IReadOnlyList<WeightedStarSystemTemplate> TemplatePool => templatePool;
        public System.Collections.Generic.IReadOnlyList<AuthoredStarSystemDefinition> AuthoredStarSystems => authoredStarSystems;
        public AuthoredStarSystemDefinition StartStarSystem => startStarSystem;
        public System.Collections.Generic.IReadOnlyList<AuthoredLane> AuthoredLanes => authoredLanes;
        public BodyTemplateDefinition DefaultHyperlaneGate => defaultHyperlaneGate;
        public BodyTemplateDefinition DefaultAncientGate => defaultAncientGate;

        // TODO(GAL-003): validate the authored world fields and cross-content references.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("GAL-003");
        }

        // TODO(TRV-003): compile and bind authored lane conditions.
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
            => throw new System.NotImplementedException("TRV-003");
        public void BindLogic(Game.Data.Logic.LogicBinder binder)
            => throw new System.NotImplementedException("TRV-003");
    }
}
