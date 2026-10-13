namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1 contract; ECO-002 owns content validation.</summary>
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
            throw new System.NotImplementedException("ECO-002");
        }
    }
}
