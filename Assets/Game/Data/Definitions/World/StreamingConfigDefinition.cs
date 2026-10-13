using System;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-31 §4.6: authored streaming limits and catalog-wide belt identity capacity.</summary>
    [ContentIdPrefix("config_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Streaming Config", fileName = "config_streaming")]
    public sealed class StreamingConfigDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private float t2UnloadFactor = 1.25f;
        [UnityEngine.SerializeField] private float bubbleRadiusM = 900f;
        [UnityEngine.SerializeField] private float bubbleUnloadFactor = 1.25f;
        [UnityEngine.SerializeField] private int maxGridSpawnsPerFrame = 2;
        [UnityEngine.SerializeField] private int maxAsteroidJobsInFlight = 4;
        [UnityEngine.SerializeField] private int maxAsteroidsPerSector = 8;
        [UnityEngine.SerializeField] private float maxRenderDistanceM = 25000f;
        [UnityEngine.SerializeField] private float interactionQueryRadiusM = 600f;
        [UnityEngine.SerializeField] private int persistDamageMaxCells = 65536;
        public float T2UnloadFactor => t2UnloadFactor;
        public float BubbleRadiusM => bubbleRadiusM;
        public float BubbleUnloadFactor => bubbleUnloadFactor;
        public int MaxGridSpawnsPerFrame => maxGridSpawnsPerFrame;
        public int MaxAsteroidJobsInFlight => maxAsteroidJobsInFlight;
        public int MaxAsteroidsPerSector => maxAsteroidsPerSector;
        public float MaxRenderDistanceM => maxRenderDistanceM;
        public float InteractionQueryRadiusM => interactionQueryRadiusM;
        public int PersistDamageMaxCells => persistDamageMaxCells;
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            Minimum(scope, "SYS-E001", nameof(t2UnloadFactor), t2UnloadFactor, 1.05f);
            Minimum(scope, "SYS-E001", nameof(bubbleUnloadFactor), bubbleUnloadFactor, 1.05f);
            Minimum(scope, "SYS-E002", nameof(bubbleRadiusM), bubbleRadiusM, 200f);
            Minimum(scope, "SYS-E004", nameof(maxRenderDistanceM), maxRenderDistanceM, float.Epsilon);
            Minimum(scope, "SYS-E004", nameof(interactionQueryRadiusM), interactionQueryRadiusM, float.Epsilon);
            Range(scope, nameof(maxGridSpawnsPerFrame), maxGridSpawnsPerFrame, 1, int.MaxValue);
            Range(scope, nameof(maxAsteroidJobsInFlight), maxAsteroidJobsInFlight, 1, int.MaxValue);
            Range(scope, nameof(maxAsteroidsPerSector), maxAsteroidsPerSector, 1, 8);
            Range(scope, nameof(persistDamageMaxCells), persistDamageMaxCells, 1, 65536);
            if (context?.Catalog == null) return;

            // Procedural belts occupy a[planets + beltSlot]. Use the largest possible centre, not a rolled sample.
            foreach (var system in context.Catalog.All<StarSystemTemplateDefinition>())
            {
                if (system.MaxBelts <= 0) continue;
                double centre = system.FirstOrbitRadiusM + ((double)system.MaxPlanets + system.MaxBelts - 1) * system.OrbitSpacingMaxM;
                for (int i = 0; i < system.BeltPool.Count; i++)
                {
                    var entry = system.BeltPool[i];
                    if (entry.template == null || entry.template.Role != BodyRole.Belt || entry.weight <= 0) continue;
                    ValidateBelt(scope, system.IdString + ".beltPool[" + i + "]", entry.template, centre);
                }
            }
            foreach (var system in context.Catalog.All<AuthoredStarSystemDefinition>())
                for (int i = 0; i < system.Bodies.Count; i++)
                {
                    var body = system.Bodies[i];
                    if (body == null || body.role != BodyRole.Belt || body.template == null) continue;
                    ValidateBelt(scope, system.IdString + ".bodies[" + i + "]", body.template, body.orbitRadiusM);
                }
        }

        static void Minimum(ValidationScope scope, string code, string field, float value, float minimum)
        {
            if (!float.IsFinite(value) || value < minimum)
                scope.Error(code, field, $"{field} must be finite and at least {minimum}.");
        }

        static void Range(ValidationScope scope, string field, int value, int minimum, int maximum)
        {
            if (value < minimum || value > maximum)
                scope.Error("SYS-E004", field, $"{field} must be within [{minimum}, {maximum}].");
        }

        static void ValidateBelt(ValidationScope scope, string field, BodyTemplateDefinition belt, double centre)
        {
            double width = belt.BeltWidthM, sectorSize = belt.SectorSizeM;
            // ring * 4096 + angularIndex must be unique and <= 8191, reserving eight ushort subslots per sector.
            double rings = Math.Ceiling(width / sectorSize);
            double inner = centre - width * 0.5;
            double lastMid = inner + (rings - 0.5) * sectorSize;
            double angles = Math.Ceiling(2 * Math.PI * lastMid / sectorSize);
            double lastSector = (rings - 1) * 4096 + angles - 1;
            if (!double.IsFinite(centre) || !double.IsFinite(width) || !double.IsFinite(sectorSize) ||
                width <= 0 || sectorSize <= 0 || inner < 0 || rings < 1 || rings > 2 || angles < 1 || lastSector > 8191)
                scope.Error("SYS-E003", field, $"Belt {belt.IdString} at centre {centre} m exceeds the SPEC-31 ring/angle sector identity bounds or has invalid dimensions. Sector * 8 + k must fit ushort without ring collisions (one ring: 8192 angles; two rings: at most 4096 per ring).");
        }
    }
}
