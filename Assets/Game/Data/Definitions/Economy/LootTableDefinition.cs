namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1: authored loot entries and ECO-E001 validation.</summary>
    [ContentIdPrefix("loot_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Loot Table", fileName = "loot_new")]
    public sealed class LootTableDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private System.Collections.Generic.List<LootEntry> entries = new();
        [UnityEngine.SerializeField, UnityEngine.Range(0, 20)] private int defaultPicks;
        public System.Collections.Generic.IReadOnlyList<LootEntry> Entries => entries;
        public int DefaultPicks => defaultPicks;
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            Game.Data.Validation.EconomyDefinitionValidation.LootEntries(entries, scope, nameof(entries), "ECO-E001");
            if (defaultPicks < 0 || defaultPicks > 20)
                scope.Error("ECO-E001", nameof(defaultPicks), "Default picks must be within [0, 20].");
        }
    }
}
