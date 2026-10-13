namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1 contract; ECO-002 owns content validation.</summary>
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
        public AudioCueDefinition SalvageCue => salvageCue;
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("ECO-002");
        }
    }
}
