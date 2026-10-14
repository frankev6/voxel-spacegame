using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Services;

namespace Game.App.Economy
{
    /// <summary>ECO-011: catalog-owned placements; authored keys take precedence over current body templates.</summary>
    public sealed class MerchantDirectory
    {
        readonly GalaxyService galaxy;
        readonly Dictionary<string, MerchantDefinition> authored = new(StringComparer.Ordinal);
        readonly Dictionary<ContentId, MerchantDefinition> templates = new();
        public MerchantDirectory(ContentCatalog catalog, GalaxyService galaxy)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            // Catalog order is ordinal; first definition wins an ambiguous placement consistently.
            foreach (var merchant in catalog.All<MerchantDefinition>())
            {
                foreach (var placement in merchant.AuthoredBodies)
                    if (placement.starSystem != null && !string.IsNullOrEmpty(placement.bodyKey))
                        authored.TryAdd(placement.starSystem.IdString + "/" + placement.bodyKey, merchant);
                foreach (var template in merchant.BodyTemplates)
                    if (template != null) templates.TryAdd(template.Id, merchant);
            }
        }
        public MerchantDefinition FindForBody(BodyId body)
        {
            if (galaxy.Galaxy == null || !galaxy.Galaxy.TryGetIndex(body.System, out _) ||
                authored.Count == 0 && templates.Count == 0) return null;
            var content = galaxy.GetContent(body.System);
            if (!content.TryFind(body.LocalKey, out int index)) return null;
            if (authored.TryGetValue(galaxy.GetLocationKey(body), out var merchant)) return merchant;
            var template = galaxy.GetBodyTemplate(content.Bodies[index].TemplateIndex);
            return templates.TryGetValue(template.Id, out merchant) ? merchant : null;
        }
    }
}
