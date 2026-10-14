using System;
using Game.Core;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Narrative;
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Dialog;
using Game.Narrative.Quests;
using Game.Narrative.ShipLog;
using Game.Galaxy.Data;

namespace Game.App.Services
{
    /// <summary>SPEC-40 §5.3: validate and bind every content owner before publishing the live narrative service.</summary>
    public static class NarrativeBootstrap
    {
        // Phase two runs after DialogPresenter subscribes: its Ended handler closes UI before beat effects.
        internal static StoryRuntime BuildStory(ContentCatalog content, NarrativeService narrative,
            NarrativeLogicAdapter adapter, DialogService dialog, ulong campaignSeed)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (narrative == null) throw new ArgumentNullException(nameof(narrative));
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (dialog == null) throw new ArgumentNullException(nameof(dialog));
            var systems = new Dictionary<ContentHash, uint>();
            foreach (var definition in content.All<AuthoredStarSystemDefinition>())
                systems.Add(definition.Id.Hash, StarSystemId.FromAuthored(definition.Id.Hash).Value);
            var tags = new Dictionary<ContentHash, string>();
            foreach (var definition in content.All<BodyTemplateDefinition>())
                tags.Add(definition.Id.Hash, definition.StoryTag ?? "");
            var quests = new QuestService(content, narrative.Facts, narrative.Context, narrative);
            var clues = new ClueService(content, narrative.Facts, narrative.Context);
            var beats = new BeatService(content, narrative.Facts, narrative.Context, dialog,
                hash => systems.TryGetValue(hash, out var value) ? value : 0,
                hash => tags.TryGetValue(hash, out var tag) ? tag : "", campaignSeed);
            adapter.BindServices(quests, clues, beats);
            narrative.Register(quests); narrative.Register(clues); narrative.Register(beats);
            return new StoryRuntime(quests, clues, beats,
                new ShipLogQuery(content, narrative.Facts, quests, clues, beats),
                new QuestSaveParticipant(content, narrative.Facts, quests, clues, beats));
        }

        internal sealed class StoryRuntime : IDisposable
        {
            internal readonly QuestService Quests;
            internal readonly ClueService Clues;
            internal readonly BeatService Beats;
            internal readonly ShipLogQuery ShipLog;
            internal readonly QuestSaveParticipant Save;
            internal StoryRuntime(QuestService quests, ClueService clues, BeatService beats,
                ShipLogQuery shipLog, QuestSaveParticipant save)
            { Quests = quests; Clues = clues; Beats = beats; ShipLog = shipLog; Save = save; }
            public void Dispose() { ShipLog.Dispose(); Beats.Dispose(); }
        }

        public static Game.Narrative.Dialog.DialogService BuildDialog(ContentCatalog content, ILogicContext context)
        {
            var programs = content.All<DialogProgramDefinition>();
            var tables = content.All<DialogCommandTableDefinition>();
            if (programs.Count != 1 || tables.Count != 1) throw new InvalidOperationException("Dialog boot requires one compiled program and command table.");
            if (programs[0].SourcesHash != tables[0].SourcesHash) throw new InvalidOperationException("Dialog program and command table have different source hashes; rebuild dialog tables.");
            var memory = new Game.Narrative.Dialog.DialogMemory();
            var runner = new Game.Narrative.Dialog.GraphDialogRunner(programs[0], context, memory);
            return new Game.Narrative.Dialog.DialogService(runner, new Game.Narrative.Dialog.DialogCommandTable(tables[0]), context, memory);
        }

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
