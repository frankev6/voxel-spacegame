using UnityEngine;

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

        // TODO(ENC-002): implement the definition checks from SPEC-33 §4.1.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("ENC-002");
        }
    }
}
