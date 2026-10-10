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

        // ---- SPEC-50 economy fields go below this line (ECO tasks). Do not edit the fields above. ----
        // ECO-001 adds: [SerializeField] private bool tradeable = true; public bool Tradeable => tradeable && Category != ItemCategory.Quest;
    }
}
