using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-33 §4.1 contract; ENC-002 owns validation and condition compilation/binding.</summary>
    [ContentIdPrefix("encounter_")]
    [CreateAssetMenu(menuName = "Game/Content/Encounter", fileName = "encounter_new")]
    public sealed class EncounterDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private string displayName;
        [SerializeField] private FactionDefinition faction;
        [SerializeField] private EncounterKind kind;
        [SerializeField] private System.Collections.Generic.List<EncounterShipEntry> ships = new();
        [SerializeField] private bool useFactionDoctrine;             // ships come from the faction doctrine instead of `ships`
        [SerializeField] private int doctrineMinShips = 2, doctrineMaxShips = 3;   // 1..10
        [SerializeField] private FormationDefinition formation;       // M4; null = line abreast (§5.6)
        [SerializeField, UnityEngine.Min(0)] private float directorWeight;          // 0 = never rolled by the director
        [SerializeField, UnityEngine.Range(0, 3)] private int minDanger, maxDanger = 3;
        [SerializeField] private bool uniquePerVisit;                 // at most once per star-system visit
        [SerializeField] private bool uniqueGlobal;                   // at most once per campaign (director only)
        [SerializeField] private Game.Data.Logic.CompiledCondition condition = new();   // LogicUsage.EncounterCondition; empty = always
        [SerializeField] private float spawnDistanceMinM = 1500f, spawnDistanceMaxM = 2500f;  // from the anchor (surface for body anchors)
        [SerializeField, UnityEngine.Min(0)] private float shipSpacingM;            // 0 = automatic (§5.6)
        [SerializeField] private bool persistUntilDefeated;           // director instances of this definition behave like story instances
        [SerializeField] private bool hailable;                       // leader gets Interactable(Hail) (§5.9)
        [SerializeField] private LootTableDefinition defeatLoot;      // SPEC-50; null = none

        public string DisplayName => displayName;
        public FactionDefinition Faction => faction;
        public EncounterKind Kind => kind;
        public System.Collections.Generic.IReadOnlyList<EncounterShipEntry> Ships => ships;
        public bool UseFactionDoctrine => useFactionDoctrine;
        public int DoctrineMinShips => doctrineMinShips;
        public int DoctrineMaxShips => doctrineMaxShips;
        public FormationDefinition Formation => formation;
        public float DirectorWeight => directorWeight;
        public int MinDanger => minDanger;
        public int MaxDanger => maxDanger;
        public bool UniquePerVisit => uniquePerVisit;
        public bool UniqueGlobal => uniqueGlobal;
        public Game.Data.Logic.CompiledCondition Condition => condition;
        public float SpawnDistanceMinM => spawnDistanceMinM;
        public float SpawnDistanceMaxM => spawnDistanceMaxM;
        public float ShipSpacingM => shipSpacingM;
        public bool PersistUntilDefeated => persistUntilDefeated;
        public bool Hailable => hailable;
        public LootTableDefinition DefeatLoot => defeatLoot;

        // TODO(ENC-002): implement the definition checks from SPEC-33 §4.1.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("ENC-002");
        }
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope) => throw new System.NotImplementedException("ENC-002");
        public void BindLogic(Game.Data.Logic.LogicBinder binder) => throw new System.NotImplementedException("ENC-002");
    }
}
