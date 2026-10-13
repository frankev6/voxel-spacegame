namespace Game.Data.Definitions
{
    /// <summary>Base inventory item. Implements SPEC-04 §4.4. Plain items are instances of this class.</summary>
    [ContentIdPrefix("item_", "resource_", "module_", "officer_", "paint_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Item", fileName = "item_new")]
    public class ItemDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;                   // player-facing, English (no localization in v1)
        [UnityEngine.SerializeField, UnityEngine.TextArea(2, 6)] private string description;
        [UnityEngine.SerializeField] private ItemCategory category = ItemCategory.Misc;
        [UnityEngine.SerializeField] private ItemRarity rarity = ItemRarity.Common;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private int basePrice;    // credits
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private int maxStack = 999; // 1 = not stackable
        [UnityEngine.SerializeField] private UnityEngine.Sprite icon;              // placeholder sprite until SPEC-81

        public string DisplayName => displayName;
        public string Description => description;
        public ItemCategory Category => category;
        public ItemRarity Rarity => rarity;
        public int BasePrice => basePrice;
        public int MaxStack => maxStack;
        public bool IsStackable => maxStack > 1;
        public UnityEngine.Sprite Icon => icon;

        /// <summary>DATA-E020…E024 and DATA-W023 (SPEC-04 §5.8). BlockDefinition calls this first.</summary>
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (string.IsNullOrWhiteSpace(displayName)) scope.Error("DATA-E020", nameof(displayName), "Display name is empty.");
            if (maxStack < 1) scope.Error("DATA-E021", nameof(maxStack), $"Max stack is {maxStack}; it must be at least 1.");
            if (basePrice < 0) scope.Error("DATA-E022", nameof(basePrice), $"Base price is {basePrice}; it must not be negative.");
            if (icon == null && IdString != "block_missing" && IdString != "item_missing")
                scope.Warning("DATA-W023", nameof(icon), "Icon is not set.");
            bool isBlock = this is BlockDefinition;
            if (isBlock && category != ItemCategory.Block)
                scope.Error("DATA-E024", nameof(category), $"A block must have category Block, not {category}.");
            else if (!isBlock && category == ItemCategory.Block)
                scope.Error("DATA-E024", nameof(category), "Only block definitions may use category Block.");
        }

        // ---- SPEC-50 economy fields go below this line (ECO tasks). Do not edit the fields above. ----
        [UnityEngine.SerializeField] private bool tradeable = true;
        public bool Tradeable => tradeable && Category != ItemCategory.Quest;
    }
}
