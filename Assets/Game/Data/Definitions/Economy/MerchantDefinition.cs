namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1 contract; ECO-002 owns content validation.</summary>
    [ContentIdPrefix("merchant_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Merchant", fileName = "merchant_new")]
    public sealed class MerchantDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;
        [UnityEngine.SerializeField] private System.Collections.Generic.List<MerchantStockEntry> stock = new();
        [UnityEngine.SerializeField] private ItemCategoryMask buys = ItemCategoryMask.Block | ItemCategoryMask.Resource | ItemCategoryMask.Module;
        [UnityEngine.SerializeField, UnityEngine.Min(0.1f)] private float sellPriceFactor = 1.0f;
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)] private float buyPriceFactor = 0.5f;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float restockHours = 24f;
        [UnityEngine.SerializeField] private System.Collections.Generic.List<BodyTemplateDefinition> bodyTemplates = new();
        [UnityEngine.SerializeField] private System.Collections.Generic.List<AuthoredBodyRef> authoredBodies = new();
        public string DisplayName => displayName;
        public System.Collections.Generic.IReadOnlyList<MerchantStockEntry> Stock => stock;
        public ItemCategoryMask Buys => buys;
        public float SellPriceFactor => sellPriceFactor;
        public float BuyPriceFactor => buyPriceFactor;
        public float RestockHours => restockHours;
        public System.Collections.Generic.IReadOnlyList<BodyTemplateDefinition> BodyTemplates => bodyTemplates;
        public System.Collections.Generic.IReadOnlyList<AuthoredBodyRef> AuthoredBodies => authoredBodies;
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("ECO-002");
        }
    }
}
