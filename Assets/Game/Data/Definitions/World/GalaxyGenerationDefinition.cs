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

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            var check = scope;
            WorldDefinitionValidation.Require(check, "GAL-E003", nameof(startStarSystem), startStarSystem != null && authoredStarSystems != null && authoredStarSystems.Contains(startStarSystem), "Start star system must belong to the authored list.");
            WorldDefinitionValidation.Positive(check, nameof(galaxyRadiusLy), galaxyRadiusLy);
            WorldDefinitionValidation.Positive(check, nameof(minStarSystemSpacingLy), minStarSystemSpacingLy);
            WorldDefinitionValidation.Positive(check, nameof(maxLaneLengthLy), maxLaneLengthLy);
            WorldDefinitionValidation.Require(check, "GAL-E017", nameof(placementAttemptsPerStarSystem), placementAttemptsPerStarSystem >= 1 && placementAttemptsPerStarSystem <= 100, "Placement attempts must be in 1..100.");
            WorldDefinitionValidation.Require(check, "GAL-E017", nameof(laneCandidateNeighbours), laneCandidateNeighbours >= 1 && laneCandidateNeighbours <= 16, "Candidate neighbours must be in 1..16.");
            WorldDefinitionValidation.Require(check, "GAL-E017", nameof(maxLaneDegree), maxLaneDegree >= 1 && maxLaneDegree <= 16, "Maximum lane degree must be in 1..16.");
            WorldDefinitionValidation.Require(check, "GAL-E017", nameof(extraLaneChance), float.IsFinite(extraLaneChance) && extraLaneChance >= 0 && extraLaneChance <= 1, "Extra lane chance must be in 0..1.");
            WorldDefinitionValidation.Require(check, "GAL-E011", nameof(templatePool), proceduralStarSystemCount <= 0 || (templatePool != null && templatePool.Count > 0), "Procedural star systems require a template pool.");
            if (templatePool != null)
                for (int i = 0; i < templatePool.Count; i++)
                {
                    var entry = templatePool[i];
                    WorldDefinitionValidation.Require(check, "GAL-E010", $"templatePool[{i}].template", entry.template != null, "Star system template must be set.");
                    WorldDefinitionValidation.Require(check, "GAL-E011", $"templatePool[{i}].weight", float.IsFinite(entry.weight) && entry.weight > 0, "Weight must be finite and positive.");
                }
            var pairs = new System.Collections.Generic.HashSet<(AuthoredStarSystemDefinition, AuthoredStarSystemDefinition)>();
            if (authoredLanes != null)
                for (int i = 0; i < authoredLanes.Count; i++)
                {
                    var lane = authoredLanes[i]; string path = $"authoredLanes[{i}]";
                    bool endsValid = lane != null && lane.a != null && lane.b != null && lane.a != lane.b;
                    WorldDefinitionValidation.Require(check, "GAL-E009", path, endsValid, "Lane ends must be set and different.");
                    if (!endsValid) continue;
                    WorldDefinitionValidation.Require(check, "GAL-E009", path, authoredStarSystems != null && authoredStarSystems.Contains(lane.a) && authoredStarSystems.Contains(lane.b), "Lane ends must belong to the authored list.");
                    WorldDefinitionValidation.Require(check, "GAL-E009", path, !pairs.Contains((lane.b, lane.a)) && pairs.Add((lane.a, lane.b)), "Lane pair must be unique, including reversed pairs.");
                    if (lane.gateTemplateA != null) WorldDefinitionValidation.Require(check, "GAL-E010", path + ".gateTemplateA", lane.gateTemplateA.Role == BodyRole.LaneGate, "Lane override must be a LaneGate template.");
                    if (lane.gateTemplateB != null) WorldDefinitionValidation.Require(check, "GAL-E010", path + ".gateTemplateB", lane.gateTemplateB.Role == BodyRole.LaneGate, "Lane override must be a LaneGate template.");
                }
            if (startStarSystem != null)
                WorldDefinitionValidation.Require(check, "GAL-E018", "startStarSystem.startBodyKey", !string.IsNullOrEmpty(startStarSystem.StartBodyKey) && startStarSystem.Bodies != null && System.Linq.Enumerable.Any(startStarSystem.Bodies, b => b != null && b.key == startStarSystem.StartBodyKey), "Start body key must name a body of the start star system.");
            WorldDefinitionValidation.Require(check, "GAL-E019", nameof(defaultHyperlaneGate), defaultHyperlaneGate != null && defaultHyperlaneGate.Role == BodyRole.LaneGate, "Default hyperlane gate must be a LaneGate template.");
            WorldDefinitionValidation.Require(check, "GAL-E019", nameof(defaultAncientGate), defaultAncientGate != null && defaultAncientGate.Role == BodyRole.LaneGate, "Default ancient gate must be a LaneGate template.");
        }

        // TODO(TRV-003): compile and bind authored lane conditions.
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
            => throw new System.NotImplementedException("TRV-003");
        public void BindLogic(Game.Data.Logic.LogicBinder binder)
            => throw new System.NotImplementedException("TRV-003");
    }
}
