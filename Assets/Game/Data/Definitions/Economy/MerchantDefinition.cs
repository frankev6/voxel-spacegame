using System;
using System.Collections.Generic;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1: merchant stock/placement fields and local validation.</summary>
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
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E002", nameof(sellPriceFactor), sellPriceFactor, .1f, float.MaxValue);
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E002", nameof(buyPriceFactor), buyPriceFactor, 0f, 1f);
            EconomyDefinitionValidation.FloatRange(scope, "ECO-E002", nameof(restockHours), restockHours, 0f, float.MaxValue);
            if (stock == null) scope.Error("ECO-E002", nameof(stock), "Merchant stock list is null.");
            else
            {
                var seen = new HashSet<ItemDefinition>();
                for (int i = 0; i < stock.Count; i++)
                {
                    var entry = stock[i];
                    string field = "stock[" + i + "]";
                    if (entry.item == null) scope.Error("ECO-E002", field + ".item", "Merchant stock item is not set.");
                    else
                    {
                        if (!seen.Add(entry.item)) scope.Error("ECO-E002", field + ".item", "Merchant stock contains a duplicate item.");
                        if (entry.item.BasePrice == 0) scope.Warning("ECO-W002", field + ".item", "Zero-price stock is sold for at least one credit.");
                    }
                    if (entry.count != -1 && entry.count < 1) scope.Error("ECO-E002", field + ".count", "Stock count must be -1 (unlimited) or at least 1.");
                    EconomyDefinitionValidation.FloatRange(scope, "ECO-E002", field + ".priceFactor", entry.priceFactor, 0f, float.MaxValue);
                }
            }
            if (authoredBodies == null) scope.Error("ECO-E003", nameof(authoredBodies), "Authored-body placement list is null.");
            else
                for (int i = 0; i < authoredBodies.Count; i++)
                {
                    var placement = authoredBodies[i];
                    string field = "authoredBodies[" + i + "]";
                    if (placement.starSystem == null)
                    { scope.Error("ECO-E003", field + ".starSystem", "Authored star system is not set."); continue; }
                    bool found = false;
                    if (placement.starSystem.Bodies != null)
                        foreach (var body in placement.starSystem.Bodies)
                            if (body != null && !string.IsNullOrEmpty(placement.bodyKey) && string.Equals(body.key, placement.bodyKey, StringComparison.Ordinal))
                            { found = true; break; }
                    if (!found) scope.Error("ECO-E003", field + ".bodyKey", "Body key must name a body in the authored star system.");
                }
        }
    }
}
