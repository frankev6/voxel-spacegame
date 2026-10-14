using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1: exact authored defaults and ECO-E005 field validation.</summary>
    [ContentIdPrefix("config_")]
    public sealed class EconomyConfigDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private int baseCargoSlots = 20;
        [UnityEngine.SerializeField] private float unitsPerSlot = 10f;
        [UnityEngine.SerializeField] private long startCredits = 2000;
        [UnityEngine.SerializeField] private System.Collections.Generic.List<LootEntry> startItems = new();
        [UnityEngine.SerializeField] private float relationPriceSlope = 0.002f;
        [UnityEngine.SerializeField] private float salvageFunctionalChance = 0.5f;
        [UnityEngine.SerializeField] private float salvageStructuralChance = 0.1f;
        [UnityEngine.SerializeField] private int salvageMaxStructuralItems = 50;
        [UnityEngine.SerializeField] private ItemDefinition scrapItem;
        [UnityEngine.SerializeField] private float scrapPerCell = 0.02f;
        [UnityEngine.SerializeField] private int salvageMaxScrap = 500;
        [UnityEngine.SerializeField] private int maxSalvageCells = 20000;
        [UnityEngine.SerializeField] private float salvageDistanceM = 120f;
        [UnityEngine.SerializeField] private float salvageReachBonusCapM = 60f;   // QA-001: summed magnet bonus cap
        [UnityEngine.SerializeField] private AudioCueDefinition salvageCue;
        public int BaseCargoSlots => baseCargoSlots;
        public float UnitsPerSlot => unitsPerSlot;
        public long StartCredits => startCredits;
        public System.Collections.Generic.IReadOnlyList<LootEntry> StartItems => startItems;
        public float RelationPriceSlope => relationPriceSlope;
        public float SalvageFunctionalChance => salvageFunctionalChance;
        public float SalvageStructuralChance => salvageStructuralChance;
        public int SalvageMaxStructuralItems => salvageMaxStructuralItems;
        public ItemDefinition ScrapItem => scrapItem;
        public float ScrapPerCell => scrapPerCell;
        public int SalvageMaxScrap => salvageMaxScrap;
        public int MaxSalvageCells => maxSalvageCells;
        public float SalvageDistanceM => salvageDistanceM;
        public float SalvageReachBonusCapM => salvageReachBonusCapM;
        public AudioCueDefinition SalvageCue => salvageCue;
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (baseCargoSlots < 1 || baseCargoSlots > 200) scope.Error("ECO-E005", nameof(baseCargoSlots), "Base cargo slots must be within [1, 200].");
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E005", nameof(unitsPerSlot), unitsPerSlot, 0f, float.MaxValue, true);
            if (startCredits < 0) scope.Error("ECO-E005", nameof(startCredits), "Starting credits must be nonnegative.");
            EconomyDefinitionValidation.LootEntries(startItems, scope, nameof(startItems), "ECO-E005");
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E005", nameof(relationPriceSlope), relationPriceSlope, 0f, float.MaxValue);
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E005", nameof(salvageFunctionalChance), salvageFunctionalChance, 0f, 1f);
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E005", nameof(salvageStructuralChance), salvageStructuralChance, 0f, 1f);
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E005", nameof(scrapPerCell), scrapPerCell, 0f, float.MaxValue);
            EconomyDefinitionValidation.Positive(scope, nameof(salvageMaxStructuralItems), salvageMaxStructuralItems);
            EconomyDefinitionValidation.Positive(scope, nameof(salvageMaxScrap), salvageMaxScrap);
            EconomyDefinitionValidation.Positive(scope, nameof(maxSalvageCells), maxSalvageCells);
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E005", nameof(salvageDistanceM), salvageDistanceM, 0f, float.MaxValue, true);
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E005", nameof(salvageReachBonusCapM), salvageReachBonusCapM, 0f, float.MaxValue, false);
            if (scrapItem == null) scope.Error("ECO-E005", nameof(scrapItem), "Scrap item is not set; canonical resource_scrap is authored by ECO-009.");
            if (salvageCue == null) scope.Error("ECO-E005", nameof(salvageCue), "Salvage cue is not set.");
        }
    }
}
