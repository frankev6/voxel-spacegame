using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Narrative;

namespace Game.App.Services
{
    /// <summary>SPEC-40 §5.3: validate and bind every content owner before publishing the live narrative service.</summary>
    public static class NarrativeBootstrap
    {
        public static NarrativeService Build(ContentCatalog content, IReadOnlyList<ContentDefinition> definitions,
            Func<FactStore, ILogicContext> createContext)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (createContext == null) throw new ArgumentNullException(nameof(createContext));
            var report = new ValidationReport("Narrative bootstrap");
            var refs = RefResolverRegistry.CreateDefault(content);
            var facts = FactCatalog.Build(content, refs, report);
            if (report.HasErrors) throw new ContentValidationException(report);
            var binder = new LogicBinder(facts, refs, report);
            var owners = new List<ContentDefinition>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition is ILogicSourceOwner &&
                    content.TryGet(definition.Id, out var canonical) && ReferenceEquals(canonical, definition)) owners.Add(definition);
            }
            owners.Sort((left, right) => StringComparer.Ordinal.Compare(left.IdString, right.IdString));
            for (int i = 0; i < owners.Count; i++) ((ILogicSourceOwner)owners[i]).BindLogic(binder);
            if (report.HasErrors) throw new ContentValidationException(report);
            var store = new FactStore(facts);
            return new NarrativeService(store, createContext(store));
        }
    }
}
