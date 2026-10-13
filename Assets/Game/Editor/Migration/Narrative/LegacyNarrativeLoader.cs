using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using Game.Data.Validation;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>One atomic, strict read of the authoritative export; never reads the old importer copies.</summary>
    internal static class LegacyNarrativeLoader
    {
        internal sealed class Sources
        {
            public readonly List<LegacyEncounterDto> Encounters = new List<LegacyEncounterDto>();
            public LegacyQuestDto[] Quests;
            public LegacyTriggerDto[] Triggers;
            public LegacyTemplateDto[] Templates;
            public LegacyHandOff Placements;
        }

        public static Sources Load(string legacyRoot, ValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            int errors = report.ErrorCount;
            var narrative = LegacyJson.ReadFile<LegacyEncounterFileDto>(legacyRoot, "narrative/NarrativeEvents.json", report);
            var story = LegacyJson.ReadFile<LegacyEncounterFileDto>(legacyRoot, "narrative/StoryEvents.json", report);
            var quests = LegacyJson.ReadFile<LegacyQuestFileDto>(legacyRoot, "narrative/QuestDefinitions.json", report);
            var triggers = LegacyJson.ReadFile<LegacyTriggerFileDto>(legacyRoot, "narrative/QuestTriggers.json", report);
            var templates = LegacyJson.ReadFile<LegacyTemplateFileDto>(legacyRoot, "narrative/MissionTemplates.json", report);
            if (report.ErrorCount != errors) return null;
            ValidateTree(narrative, "narrative/NarrativeEvents.json", "", report);
            ValidateTree(story, "narrative/StoryEvents.json", "", report);
            ValidateTree(quests, "narrative/QuestDefinitions.json", "", report);
            ValidateTree(triggers, "narrative/QuestTriggers.json", "", report);
            ValidateTree(templates, "narrative/MissionTemplates.json", "", report);
            if (report.ErrorCount != errors) return null;
            var result = new Sources { Quests = quests.Quests, Triggers = triggers.Triggers, Templates = templates.Templates };
            Add(narrative.Encounters, "narrative/NarrativeEvents.json");
            Add(story.Encounters, "narrative/StoryEvents.json");
            for (int i = 0; i < result.Quests.Length; i++) result.Quests[i].SourcePath = "narrative/QuestDefinitions.json:quests[" + i + "]";
            for (int i = 0; i < result.Triggers.Length; i++) result.Triggers[i].SourcePath = "narrative/QuestTriggers.json:triggers[" + i + "]";
            for (int i = 0; i < result.Templates.Length; i++) result.Templates[i].SourcePath = "narrative/MissionTemplates.json:templates[" + i + "]";
            // SPEC-72 already performs strict type dispatch and resolves pool links through the export/ID map.
            var index = LegacyContentIndex.Load(legacyRoot, report);
            result.Placements = LegacyHandOff.Build(index, LegacyIdMap.Default, report);
            return report.ErrorCount == errors ? result : null;

            void Add(LegacyEncounterDto[] rows, string path)
            {
                for (int i = 0; i < rows.Length; i++)
                { rows[i].SourcePath = path + ":encounters[" + i + "]"; result.Encounters.Add(rows[i]); }
            }
        }
        // Newtonsoft validates properties; array elements need this additional shape check before
        // assigning source paths. All children remain typed DTOs, never extension-data dictionaries.
        static void ValidateTree(object value, string path, string field, ValidationReport report)
        {
            if (value == null) { report.Error("MIG-E002", path, field, "Null legacy row."); return; }
            if (value is string || value.GetType().IsValueType) return;
            if (value is IEnumerable rows)
            {
                int i = 0;
                foreach (object row in rows) ValidateTree(row, path, field + "[" + i++ + "]", report);
                return;
            }
            foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name == "SourcePath") continue;
                var attribute = property.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>();
                string child = (field.Length == 0 ? "" : field + ".") + attribute.PropertyName;
                object row = property.GetValue(value);
                if (property.Name == "Id" && string.IsNullOrWhiteSpace(row as string))
                    report.Error("MIG-E002", path, child, "Missing legacy ID.");
                else if (row != null) ValidateTree(row, path, child, report);
            }
        }
    }
}
