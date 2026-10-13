using UnityEngine;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-33 §4.1 contract; ENC-002 owns validation.</summary>
    [ContentIdPrefix("config_")]
    public sealed class EncounterConfigDefinition : ContentDefinition
    {
        [SerializeField] private float firstRollDelaySeconds = 90f;
        [SerializeField] private float rollIntervalSeconds = 120f;
        [SerializeField] private float[] dangerSpawnChance = { 0f, 0.25f, 0.45f, 0.65f };   // index = StarSystemDef.Danger
        [SerializeField] private int maxActiveDirectorInstances = 2;      // per star system
        [SerializeField] private float minPlayerDistanceM = 800f;         // director and narrative spawns, except StartCombat
        [SerializeField] private float combatMinDistanceM = 300f;         // StartCombat
        [SerializeField] private float arrivalGraceSeconds = 10f;         // after StarSystemEntered(Jump): no director roll
        [SerializeField] private float arrivalGraceDistanceM = 2000f;     // … and no director spawn closer than this to the arrival point
        [SerializeField] private int placementTries = 12;
        [SerializeField] private float placementStepDegrees = 30f;
        [SerializeField] private float bodyClearanceM = 150f;
        [SerializeField] private float despawnDistanceM = 6000f;
        [SerializeField] private float despawnDelaySeconds = 30f;
        [SerializeField] private int maxInstances = 16;
        [SerializeField] private int maxShipsAlive = 40;
        [SerializeField] private int hostileAtOrBelow = -25;
        [SerializeField] private int friendlyAtOrAbove = 50;
        [SerializeField] private int provokePenalty = 10;
        [SerializeField] private int provokeIntervalTicks = 60;
        [SerializeField] private float hailDistanceM = 300f;

        public float FirstRollDelaySeconds => firstRollDelaySeconds;
        public float RollIntervalSeconds => rollIntervalSeconds;
        public System.Collections.Generic.IReadOnlyList<float> DangerSpawnChance => dangerSpawnChance;
        public int MaxActiveDirectorInstances => maxActiveDirectorInstances;
        public float MinPlayerDistanceM => minPlayerDistanceM;
        public float CombatMinDistanceM => combatMinDistanceM;
        public float ArrivalGraceSeconds => arrivalGraceSeconds;
        public float ArrivalGraceDistanceM => arrivalGraceDistanceM;
        public int PlacementTries => placementTries;
        public float PlacementStepDegrees => placementStepDegrees;
        public float BodyClearanceM => bodyClearanceM;
        public float DespawnDistanceM => despawnDistanceM;
        public float DespawnDelaySeconds => despawnDelaySeconds;
        public int MaxInstances => maxInstances;
        public int MaxShipsAlive => maxShipsAlive;
        public int HostileAtOrBelow => hostileAtOrBelow;
        public int FriendlyAtOrAbove => friendlyAtOrAbove;
        public int ProvokePenalty => provokePenalty;
        public int ProvokeIntervalTicks => provokeIntervalTicks;
        public float HailDistanceM => hailDistanceM;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (dangerSpawnChance == null || dangerSpawnChance.Length != 4) scope.Error("ENC-E020", nameof(dangerSpawnChance), "Danger spawn chance requires four entries.");
            else for (int i = 0; i < dangerSpawnChance.Length; i++)
                if (!float.IsFinite(dangerSpawnChance[i]) || dangerSpawnChance[i] < 0 || dangerSpawnChance[i] > 1)
                    scope.Error("ENC-E020", "dangerSpawnChance[" + i + "]", "Danger spawn chance must be finite and within [0, 1].");
            if (hostileAtOrBelow >= friendlyAtOrAbove) scope.Error("ENC-E020", "hostileAtOrBelow/friendlyAtOrAbove", "Hostile threshold must be below friendly threshold.");
            EncounterDefinitionValidation.Positive(scope, nameof(firstRollDelaySeconds), firstRollDelaySeconds);
            EncounterDefinitionValidation.Positive(scope, nameof(rollIntervalSeconds), rollIntervalSeconds);
            EncounterDefinitionValidation.Positive(scope, nameof(maxActiveDirectorInstances), maxActiveDirectorInstances);
            EncounterDefinitionValidation.Positive(scope, nameof(minPlayerDistanceM), minPlayerDistanceM);
            EncounterDefinitionValidation.Positive(scope, nameof(combatMinDistanceM), combatMinDistanceM);
            EncounterDefinitionValidation.Positive(scope, nameof(arrivalGraceSeconds), arrivalGraceSeconds);
            EncounterDefinitionValidation.Positive(scope, nameof(arrivalGraceDistanceM), arrivalGraceDistanceM);
            EncounterDefinitionValidation.Positive(scope, nameof(placementTries), placementTries);
            EncounterDefinitionValidation.Positive(scope, nameof(placementStepDegrees), placementStepDegrees);
            EncounterDefinitionValidation.Positive(scope, nameof(bodyClearanceM), bodyClearanceM);
            EncounterDefinitionValidation.Positive(scope, nameof(despawnDistanceM), despawnDistanceM);
            EncounterDefinitionValidation.Positive(scope, nameof(despawnDelaySeconds), despawnDelaySeconds);
            EncounterDefinitionValidation.Positive(scope, nameof(maxInstances), maxInstances);
            EncounterDefinitionValidation.Positive(scope, nameof(maxShipsAlive), maxShipsAlive);
            if (maxShipsAlive > 60) scope.Error("ENC-E020", nameof(maxShipsAlive), "At most 60 encounter ships may be alive.");
            EncounterDefinitionValidation.Positive(scope, nameof(provokePenalty), provokePenalty);
            EncounterDefinitionValidation.Positive(scope, nameof(provokeIntervalTicks), provokeIntervalTicks);
            EncounterDefinitionValidation.Positive(scope, nameof(hailDistanceM), hailDistanceM);
        }
    }
}
