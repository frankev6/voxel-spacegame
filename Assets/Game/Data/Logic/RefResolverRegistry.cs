using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Data.Logic
{
    /// <summary>Canonical content-family resolvers; explicit registration replaces a family's resolver.</summary>
    public sealed class RefResolverRegistry
    {
        readonly Dictionary<RefKind, IRefResolver> resolvers = new Dictionary<RefKind, IRefResolver>();
        public static RefResolverRegistry CreateDefault(ContentCatalog content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var registry = new RefResolverRegistry();
            registry.Register(new CatalogRefResolver<ItemDefinition>(RefKind.Item, content));
            registry.Register(new CatalogRefResolver<QuestDefinition>(RefKind.Quest, content));
            registry.Register(new CatalogRefResolver<ClueDefinition>(RefKind.Clue, content));
            registry.Register(new CatalogRefResolver<StoryBeatDefinition>(RefKind.Beat, content));
            registry.Register(new CatalogRefResolver<MysteryDefinition>(RefKind.Mystery, content));
            registry.Register(new CatalogRefResolver<LootTableDefinition>(RefKind.LootTable, content));
            registry.Register(new CatalogRefResolver<MerchantDefinition>(RefKind.Merchant, content));
            registry.Register(new CatalogRefResolver<AuthoredStarSystemDefinition>(RefKind.StarSystem, content));
            registry.Register(new CatalogRefResolver<FactionDefinition>(RefKind.Faction, content));
            registry.Register(new CatalogRefResolver<EncounterDefinition>(RefKind.Encounter, content));
            registry.Register(new CatalogRefResolver<ShipBlueprintDefinition>(RefKind.Blueprint, content));
            registry.Register(new CatalogRefResolver<AudioCueDefinition>(RefKind.AudioCue, content));
            registry.Register(new CatalogRefResolver<BodyTemplateDefinition>(RefKind.Entity, content));
            return registry;
        }
        public void Register(IRefResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            resolvers[resolver.Kind] = resolver;
        }
        public bool TryGet(RefKind kind, out IRefResolver resolver) => resolvers.TryGetValue(kind, out resolver);
    }
}
