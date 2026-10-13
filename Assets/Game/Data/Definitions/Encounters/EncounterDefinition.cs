using UnityEngine;
using System;
using Game.Data.Validation;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;

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

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (faction == null) scope.Error("ENC-E010", nameof(faction), "Encounter faction is not set.");
            bool doctrineRange = EncounterDefinitionValidation.ShipRange(scope, "doctrineMinShips/doctrineMaxShips", doctrineMinShips, doctrineMaxShips);
            if (useFactionDoctrine)
            {
                if (faction != null && (faction.Doctrine == null || faction.Doctrine.Count == 0))
                    scope.Error("ENC-E011", nameof(useFactionDoctrine), "Doctrine encounter requires a nonempty faction doctrine.");
            }
            else if (ships == null || ships.Count == 0) scope.Error("ENC-E011", nameof(ships), "Encounter must contain at least one ship entry.");
            long total = 0;
            if (ships != null)
                for (int i = 0; i < ships.Count; i++)
                {
                    var entry = ships[i];
                    if (!useFactionDoctrine && entry.blueprint == null) scope.Error("ENC-E011", "ships[" + i + "].blueprint", "Encounter blueprint is not set.");
                    if (EncounterDefinitionValidation.ShipRange(scope, "ships[" + i + "]", entry.countMin, entry.countMax)) total += entry.countMax;
                }
            if (!useFactionDoctrine && total > 10) scope.Error("ENC-E012", nameof(ships), "Maximum total encounter ships exceeds 10.");
            if (minDanger < 0 || minDanger > maxDanger || maxDanger > 3) scope.Error("ENC-E013", "minDanger/maxDanger", "Danger range must be within [0, 3].");
            EncounterDefinitionValidation.Nonnegative(scope, "ENC-E013", nameof(directorWeight), directorWeight);
            if (!float.IsFinite(spawnDistanceMinM) || !float.IsFinite(spawnDistanceMaxM) || spawnDistanceMinM < 100 || spawnDistanceMinM > spawnDistanceMaxM || spawnDistanceMaxM > 10000)
                scope.Error("ENC-E014", "spawnDistanceMinM/spawnDistanceMaxM", "Spawn distances must satisfy 100 <= minimum <= maximum <= 10000 m.");
            EncounterDefinitionValidation.Nonnegative(scope, "ENC-E014", nameof(shipSpacingM), shipSpacingM);
            if (kind != EncounterKind.Merchant || faction == null) return;
            var aiConfigs = context?.Catalog?.All<AiConfigDefinition>();
            AiProfileDefinition fallback = aiConfigs != null && aiConfigs.Count == 1 ? aiConfigs[0].DefaultProfile : null;
            if (useFactionDoctrine)
            {
                if (faction.Doctrine == null || !doctrineRange) return;
                for (int i = 0; i < faction.Doctrine.Count; i++)
                    Civilian(scope, "faction.doctrine[" + i + "].aiProfile", faction.Doctrine[i].aiProfile ?? faction.DefaultAiProfile ?? fallback);
            }
            else if (ships != null)
                for (int i = 0; i < ships.Count; i++)
                    Civilian(scope, "ships[" + i + "].aiProfile", ships[i].aiProfile ?? faction.DefaultAiProfile ?? fallback);
        }
        static void Civilian(ValidationScope scope, string field, AiProfileDefinition profile)
        {
            if (profile == null || profile.Role != AiRole.Civilian) scope.Error("ENC-E015", field, "Merchant's resolved AI profile must be Civilian.");
        }
        public void CompileLogic(LogicCompiler compiler, ValidationScope scope)
        {
            if (compiler == null) throw new ArgumentNullException(nameof(compiler));
            condition ??= new CompiledCondition();
            var report = new ValidationReport("Encounter condition");
            compiler.Compile(condition, new LogicCompileOptions(LogicUsage.EncounterCondition, scope.AssetPath, "condition"), report);
            foreach (var entry in report.Entries)
                switch (entry.Severity)
                {
                    case ValidationSeverity.Error: scope.Error(entry.Code, entry.Field, entry.Message); break;
                    case ValidationSeverity.Warning: scope.Warning(entry.Code, entry.Field, entry.Message); break;
                    default: scope.Info(entry.Code, entry.Field, entry.Message); break;
                }
        }
        public void BindLogic(LogicBinder binder)
        {
            if (binder == null) throw new ArgumentNullException(nameof(binder));
            condition ??= new CompiledCondition();
            condition.Bind(binder);
        }
    }
}
