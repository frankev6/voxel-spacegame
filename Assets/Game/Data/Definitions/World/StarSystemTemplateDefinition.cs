using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [ContentIdPrefix("systpl_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Star System Template", fileName = "systpl_new")]
    public sealed class StarSystemTemplateDefinition : ContentDefinition
    {
        [SerializeField] private string namePrefix = "Uncharted";      // procedural display name prefix (§5.7)
        [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> starPool = new();
        [SerializeField] private int minPlanets = 2, maxPlanets = 4;
        [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> planetPool = new();
        [SerializeField] private int maxMoonsPerPlanet = 2;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float moonChance = 0.5f;
        [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> moonPool = new();
        [SerializeField] private int minBelts = 0, maxBelts = 1;
        [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> beltPool = new();
        [SerializeField] private int minStations = 0, maxStations = 1;
        [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> stationPool = new();
        [SerializeField] private int minPointsOfInterest = 1, maxPointsOfInterest = 3;
        [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> pointOfInterestPool = new();
        [SerializeField] private float firstOrbitRadiusM = 3000f;
        [SerializeField] private float orbitSpacingMinM = 2000f, orbitSpacingMaxM = 3000f;
        [SerializeField] private double orbitPeriodAtReferenceS = 3600.0;   // Kepler-lite: T = Tref·(a/aref)^1.5
        [SerializeField] private float orbitReferenceRadiusM = 4000f;
        [SerializeField] private float moonOrbitMinM = 600f, moonOrbitMaxM = 1500f;   // added to the planet radius
        [SerializeField] private double moonPeriodAtReferenceS = 600.0;
        [SerializeField] private float moonReferenceRadiusM = 1000f;
        [SerializeField] private float stationRadiusMinM = 2500f, stationRadiusMaxM = 9000f;
        [SerializeField] private float pointOfInterestRadiusMinM = 2000f, pointOfInterestRadiusMaxM = 12000f;
        [SerializeField] private float minBodySeparationM = 1500f;
        [SerializeField] private float laneRingMarginM = 3000f;
        [SerializeField] private float minLaneRingRadiusM = 8000f;
        [SerializeField] private float extentMarginM = 2000f;              // ExtentM = lane ring radius + this
        [SerializeField] private int dangerMin = 0, dangerMax = 1;          // 0..3

        public string NamePrefix => namePrefix;
        public System.Collections.Generic.IReadOnlyList<WeightedBodyTemplate> StarPool => starPool;
        public int MinPlanets => minPlanets;
        public int MaxPlanets => maxPlanets;
        public System.Collections.Generic.IReadOnlyList<WeightedBodyTemplate> PlanetPool => planetPool;
        public int MaxMoonsPerPlanet => maxMoonsPerPlanet;
        public float MoonChance => moonChance;
        public System.Collections.Generic.IReadOnlyList<WeightedBodyTemplate> MoonPool => moonPool;
        public int MinBelts => minBelts;
        public int MaxBelts => maxBelts;
        public System.Collections.Generic.IReadOnlyList<WeightedBodyTemplate> BeltPool => beltPool;
        public int MinStations => minStations;
        public int MaxStations => maxStations;
        public System.Collections.Generic.IReadOnlyList<WeightedBodyTemplate> StationPool => stationPool;
        public int MinPointsOfInterest => minPointsOfInterest;
        public int MaxPointsOfInterest => maxPointsOfInterest;
        public System.Collections.Generic.IReadOnlyList<WeightedBodyTemplate> PointOfInterestPool => pointOfInterestPool;
        public float FirstOrbitRadiusM => firstOrbitRadiusM;
        public float OrbitSpacingMinM => orbitSpacingMinM;
        public float OrbitSpacingMaxM => orbitSpacingMaxM;
        public double OrbitPeriodAtReferenceS => orbitPeriodAtReferenceS;
        public float OrbitReferenceRadiusM => orbitReferenceRadiusM;
        public float MoonOrbitMinM => moonOrbitMinM;
        public float MoonOrbitMaxM => moonOrbitMaxM;
        public double MoonPeriodAtReferenceS => moonPeriodAtReferenceS;
        public float MoonReferenceRadiusM => moonReferenceRadiusM;
        public float StationRadiusMinM => stationRadiusMinM;
        public float StationRadiusMaxM => stationRadiusMaxM;
        public float PointOfInterestRadiusMinM => pointOfInterestRadiusMinM;
        public float PointOfInterestRadiusMaxM => pointOfInterestRadiusMaxM;
        public float MinBodySeparationM => minBodySeparationM;
        public float LaneRingMarginM => laneRingMarginM;
        public float MinLaneRingRadiusM => minLaneRingRadiusM;
        public float ExtentMarginM => extentMarginM;
        public int DangerMin => dangerMin;
        public int DangerMax => dangerMax;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            WorldDefinitionValidation.Counts(scope, nameof(minPlanets), minPlanets, maxPlanets);
            WorldDefinitionValidation.Counts(scope, nameof(minBelts), minBelts, maxBelts);
            WorldDefinitionValidation.Counts(scope, nameof(minStations), minStations, maxStations);
            WorldDefinitionValidation.Counts(scope, nameof(minPointsOfInterest), minPointsOfInterest, maxPointsOfInterest);
            WorldDefinitionValidation.Counts(scope, nameof(maxMoonsPerPlanet), 0, maxMoonsPerPlanet);
            WorldDefinitionValidation.Require(scope, "GAL-E012", nameof(dangerMin), dangerMin >= 0 && dangerMax >= dangerMin && dangerMax <= 3, "Danger must be ordered in 0..3.");
            WorldDefinitionValidation.Require(scope, "GAL-E012", nameof(moonChance), float.IsFinite(moonChance) && moonChance >= 0 && moonChance <= 1, "Moon chance must be in 0..1.");
            WorldDefinitionValidation.Pool(scope, nameof(starPool), starPool, BodyRole.Star, 1);
            WorldDefinitionValidation.Pool(scope, nameof(planetPool), planetPool, BodyRole.Planet, minPlanets);
            WorldDefinitionValidation.Pool(scope, nameof(moonPool), moonPool, BodyRole.Moon, 0);
            WorldDefinitionValidation.Pool(scope, nameof(beltPool), beltPool, BodyRole.Belt, minBelts);
            WorldDefinitionValidation.Pool(scope, nameof(stationPool), stationPool, BodyRole.Station, minStations);
            WorldDefinitionValidation.Pool(scope, nameof(pointOfInterestPool), pointOfInterestPool, BodyRole.PointOfInterest, minPointsOfInterest);
            WorldDefinitionValidation.Range(scope, nameof(orbitSpacingMinM), orbitSpacingMinM, orbitSpacingMaxM);
            WorldDefinitionValidation.Range(scope, nameof(moonOrbitMinM), moonOrbitMinM, moonOrbitMaxM);
            WorldDefinitionValidation.Range(scope, nameof(stationRadiusMinM), stationRadiusMinM, stationRadiusMaxM);
            WorldDefinitionValidation.Range(scope, nameof(pointOfInterestRadiusMinM), pointOfInterestRadiusMinM, pointOfInterestRadiusMaxM);
            WorldDefinitionValidation.Positive(scope, nameof(firstOrbitRadiusM), firstOrbitRadiusM);
            WorldDefinitionValidation.Positive(scope, nameof(orbitPeriodAtReferenceS), orbitPeriodAtReferenceS);
            WorldDefinitionValidation.Positive(scope, nameof(orbitReferenceRadiusM), orbitReferenceRadiusM);
            WorldDefinitionValidation.Positive(scope, nameof(moonPeriodAtReferenceS), moonPeriodAtReferenceS);
            WorldDefinitionValidation.Positive(scope, nameof(moonReferenceRadiusM), moonReferenceRadiusM);
            WorldDefinitionValidation.Positive(scope, nameof(minBodySeparationM), minBodySeparationM);
            WorldDefinitionValidation.Positive(scope, nameof(laneRingMarginM), laneRingMarginM);
            WorldDefinitionValidation.Positive(scope, nameof(minLaneRingRadiusM), minLaneRingRadiusM);
            WorldDefinitionValidation.Positive(scope, nameof(extentMarginM), extentMarginM);
        }
    }
}
