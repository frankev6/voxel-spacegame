using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;

namespace Game.Editor.Content.Validators
{
    /// <summary>SPEC-50 ECO-002: database cardinality, merchant placement ownership and raw quest tradeability.</summary>
    public sealed class EconomyContentValidator : IContentDatabaseValidator
    {
        public int Order => 500;

        public void Validate(ContentCatalog catalog, ValidationReport report)
        {
            var configs = catalog.All<EconomyConfigDefinition>();
            if (configs.Count != 1)
                report.Error("ECO-E005", configs.Count > 1 ? Path(configs[1]) : "Assets/Game/Content/Config", "",
                    $"There must be exactly one EconomyConfigDefinition (config_economy); found {configs.Count}.");
            else if (configs[0].IdString != "config_economy")
                report.Error("ECO-E005", Path(configs[0]), "id", "The economy config must have ID config_economy.");

            var templateOwners = new Dictionary<BodyTemplateDefinition, MerchantDefinition>();
            var authoredOwners = new Dictionary<(AuthoredStarSystemDefinition, string), MerchantDefinition>();
            foreach (var merchant in catalog.All<MerchantDefinition>())
            {
                if (merchant.BodyTemplates != null)
                    for (int i = 0; i < merchant.BodyTemplates.Count; i++)
                    {
                        var template = merchant.BodyTemplates[i];
                        if (template == null) continue;
                        if (templateOwners.TryGetValue(template, out var owner) && owner != merchant)
                            report.Error("ECO-E004", Path(merchant), "bodyTemplates[" + i + "]", "Body template " + template.IdString + " is already claimed by merchant " + owner.IdString + ".");
                        else templateOwners[template] = merchant;
                    }
                if (merchant.AuthoredBodies != null)
                    for (int i = 0; i < merchant.AuthoredBodies.Count; i++)
                    {
                        var placement = merchant.AuthoredBodies[i];
                        if (!HasAuthoredBody(placement)) continue; // ECO-E003 owns invalid refs/keys.
                        var key = (placement.starSystem, placement.bodyKey);
                        if (authoredOwners.TryGetValue(key, out var owner) && owner != merchant)
                            report.Error("ECO-E004", Path(merchant), "authoredBodies[" + i + "]", "Authored body " + placement.starSystem.IdString + "/" + placement.bodyKey + " is already claimed by merchant " + owner.IdString + ".");
                        else authoredOwners[key] = merchant;
                    }
            }
            // Tradeable deliberately hides the authored true value on quests. Inspect it here without expanding the public API.
            foreach (var item in catalog.All<ItemDefinition>())
            {
                if (item.Category != ItemCategory.Quest) continue;
                using var so = new SerializedObject(item);
                if (so.FindProperty("tradeable").boolValue)
                    report.Warning("ECO-W001", Path(item), "tradeable", "Quest item is marked tradeable; the flag is ignored and it cannot be traded.");
            }
        }

        static bool HasAuthoredBody(AuthoredBodyRef placement)
        {
            if (placement.starSystem == null || string.IsNullOrEmpty(placement.bodyKey) || placement.starSystem.Bodies == null) return false;
            foreach (var body in placement.starSystem.Bodies)
                if (body != null && string.Equals(body.key, placement.bodyKey, System.StringComparison.Ordinal)) return true;
            return false;
        }

        static string Path(ContentDefinition definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(path) ? "content:" + definition.IdString : path;
        }
    }
}
