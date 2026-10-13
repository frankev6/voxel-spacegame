using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Validation;
using UnityEditor;
using static Game.Editor.Migration.Narrative.LegacyNarrativeMapping;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>SPEC-40 §5.8: typed, source-preserving quest/contract conversion before any writes.</summary>
    internal static class LegacyQuestConverter
    {
        internal sealed class Objective
        {
            internal string Id, Text, Condition, Progress = "";
            internal int Target;
            internal bool Latch = true;
            internal QuestMarker Marker = new();
        }
        internal sealed class Stage
        {
            internal string Id, Title;
            internal readonly List<Objective> Objectives = new();
            internal readonly List<string> OnComplete = new();
        }
        internal sealed class Quest
        {
            internal string Id, Title, Short, Description;
            internal QuestKind Kind;
            internal bool AutoTrack;
            internal readonly List<Stage> Stages = new();
            internal readonly List<string> Accepted = new(), Completed = new(), Failed = new();
            internal readonly List<ConvertedEncounter.LogEntry> Logs = new();
        }
        internal sealed class Beat
        {
            internal string Id, Condition, Node = "", Tag = "";
            internal BeatTrigger Trigger;
            internal int Salience;
            internal bool FirstVisit;
            internal BeatRepeat Repeat = BeatRepeat.Always;
            internal readonly List<string> OnStart = new();
        }
        internal sealed class Plan
        {
            internal readonly List<Quest> Quests = new();
            internal readonly List<Beat> Beats = new();
            internal LegacyClueWriter.Plan Clues;
            internal IEnumerable<string> AssetPaths(string root) => Quests.Select(q => root + "/Quests/" + q.Id + ".asset")
                .Concat(Beats.Select(b => root + "/Beats/" + b.Id + ".asset")).Concat(Clues.AssetPaths(root));
            internal void CheckTypes(string root)
            {
                foreach (var q in Quests) LegacyNarrativeConverter.CheckType<QuestDefinition>(root + "/Quests/" + q.Id + ".asset");
                foreach (var b in Beats) LegacyNarrativeConverter.CheckType<StoryBeatDefinition>(root + "/Beats/" + b.Id + ".asset");
                Clues.CheckTypes(root);
            }
            internal void Write(LegacyAssetWriter writer, string root, ValidationReport report)
            {
                foreach (var q in Quests) WriteQuest(writer, q);
                foreach (var b in Beats) WriteBeat(writer, b);
                Clues.Write(writer);
            }
        }

        internal static Plan Prepare(LegacyNarrativeLoader.Sources sources, LegacyActionTranslator translator,
            LegacyNarrativeConverter.Plan dialogue, Func<string, ContentDefinition> resolve, ValidationReport report)
        {
            var result = new Plan(); var quests = new Dictionary<string, Quest>(StringComparer.Ordinal);
            var dialogueLogs = translator.Logs.ToArray();
            // SPEC-42 rumor resolution reads this authored opening-chapter fact; its writer is QST-009.
            translator.DeclareFact(Declaration("opening_cache_found", FactKind.Bool));
            foreach (var source in sources.Quests)
            {
                string id = QuestId(source.Id); bool chain = id == "quest_whisper_chain";
                if (!quests.TryGetValue(id, out var quest))
                {
                    quest = new Quest { Id = id, Title = source.Title, Short = source.ShortDescription, Description = source.DetailedDescription,
                        AutoTrack = source.AutoTrack, Kind = id == "quest_debug_fail_sandbox" ? QuestKind.Debug : source.IsStory ? QuestKind.Story : QuestKind.Side };
                    quests.Add(id, quest); result.Quests.Add(quest);
                }
                else
                { quest.Short += "\n" + source.ShortDescription; quest.Description += "\n" + source.DetailedDescription; }
                int index = quest.Stages.Count;
                var stage = new Stage { Id = chain ? new[] { "triangulate", "intercept", "debrief" }[index] : "main", Title = source.Title };
                quest.Stages.Add(stage);
                string asset = dialogue.Root + "/Quests/" + id + ".asset", stageField = "stages[" + index + "]";
                Field(source.SourcePath + "/title", source.Title, asset, chain ? stageField + ".title" : "title");
                Field(source.SourcePath + "/shortDescription", source.ShortDescription, asset, "shortDescription");
                Field(source.SourcePath + "/detailedDescription", source.DetailedDescription, asset, "description");
                ConvertObjectives(source.Objectives, source.Markers, stage, index, source.SourcePath, asset);
                var accepted = Actions(source.OnAcceptedActions, source.Id, source.SourcePath + "/onAcceptedActions", asset, quest);
                if (index == 0) quest.Accepted.AddRange(accepted);
                else quest.Stages[index - 1].OnComplete.AddRange(accepted); // next-stage acceptance follows previous-stage completion
                var completed = Actions(source.OnCompletedActions, source.Id, source.SourcePath + "/onCompletedActions", asset, quest);
                stage.OnComplete.AddRange(completed);
                var failed = Actions(source.OnFailedActions, source.Id, source.SourcePath + "/onFailedActions", asset, quest);
                if (!chain) quest.Failed.AddRange(failed);
                else if (failed.Count != 0)
                {
                    // QuestStage has no onFailed list. Existing condition beats select the actual failed
                    // stage instead of executing all three mutually exclusive legacy failure logs.
                    var failure = new Beat { Id = "beat_whisper_chain_failed_" + stage.Id, Trigger = BeatTrigger.Condition,
                        Condition = "quest_failed(\"quest_whisper_chain\") and quest_stage(\"quest_whisper_chain\") == " + index,
                        Repeat = BeatRepeat.Once };
                    failure.OnStart.AddRange(failed); result.Beats.Add(failure);
                }
                foreach (var option in source.EntityOptions.Select((value, i) => (value, i))) EntityOption(source, option.value, option.i);
            }
            foreach (var source in sources.Templates)
            {
                string id = QuestId(source.Id), asset = dialogue.Root + "/Quests/" + id + ".asset";
                var quest = new Quest { Id = id, Title = source.TitleFormat, Short = source.ShortDescriptionFormat,
                    Description = source.DetailedDescriptionFormat, AutoTrack = source.AutoTrack, Kind = QuestKind.Contract };
                quests.Add(id, quest); result.Quests.Add(quest);
                var stage = new Stage { Id = "main", Title = "" }; quest.Stages.Add(stage);
                Field(source.SourcePath + "/titleFormat", source.TitleFormat, asset, "title");
                Field(source.SourcePath + "/shortDescriptionFormat", source.ShortDescriptionFormat, asset, "shortDescription");
                Field(source.SourcePath + "/detailedDescriptionFormat", source.DetailedDescriptionFormat, asset, "description");
                ConvertObjectives(source.Objectives, source.Markers, stage, 0, source.SourcePath, asset);
                quest.Accepted.AddRange(Actions(source.OnAcceptedActions, source.Id, source.SourcePath + "/onAcceptedActions", asset, quest));
                quest.Completed.AddRange(Actions(source.OnCompletedActions, source.Id, source.SourcePath + "/onCompletedActions", asset, quest));
            }
            foreach (var row in sources.Triggers)
            {
                if (row.Id == "trigger_station_boarding_followup" || row.Id == "trigger_fail_sandbox_on_relation_drop")
                {
                    dialogue.Manifest.AddDropped(row.SourcePath, row.Id, dialogue.ManifestPath,
                        row.Id == "trigger_station_boarding_followup" ? "Duplicate quest onCompleted acceptance (05-B15)" : "Dropped debug relation trigger");
                    continue;
                }
                bool friendly = row.Id == "trigger_first_friendly_territory";
                if (!friendly && row.Id != "trigger_first_hostile_territory") throw new FormatException("Unmapped trigger: " + row.Id);
                var beat = new Beat { Id = friendly ? "beat_territory_friendly_entry" : "beat_territory_hostile_entry",
                    Trigger = BeatTrigger.StarSystemEntered, FirstVisit = true,
                    Condition = "star_system_owned() and relation(\"owner\") " + (friendly ? ">= 0" : "< 0"),
                    Node = dialogue.Encounters.Single(e => e.BeatId == (friendly ? "beat_territory_friendly_entry" : "beat_territory_hostile_entry")).Nodes[0].Title,
                    Salience = LegacyBeatWriter.GetPlacement(friendly ? "event_territory_friendly_entry" : "event_territory_hostile_entry").Salience };
                foreach (var action in row.Actions)
                {
                    // The trigger itself is the existing territory beat: queuing itself would recurse.
                    if (action.ActionType == "QueueEncounter" && BeatId(action.EncounterId) == beat.Id) continue;
                    beat.OnStart.AddRange(translator.TranslateQuestAction(action, null).Commands);
                }
                result.Beats.Add(beat);
            }
            foreach (var quest in result.Quests)
                quest.Logs.AddRange(translator.Logs.Where(log => log.QuestId == quest.Id));
            int dialogueLog = 0;
            foreach (var encounter in sources.Encounters)
                for (int page = 0; page < encounter.Pages.Length; page++)
                    for (int option = 0; option < encounter.Pages[page].Options.Length; option++)
                        if (encounter.Pages[page].Options[option].Actions.Contains("quest.add_log", StringComparison.Ordinal))
                        {
                            if (dialogueLog >= dialogueLogs.Length) throw new FormatException("Dialogue log provenance differs from translated output.");
                            var log = dialogueLogs[dialogueLog++];
                            Field(encounter.SourcePath + "/" + encounter.Id + "/pages[" + page + "]/options[" + option + "]/actions/quest.add_log.text",
                                log.Text, dialogue.Root + "/Quests/" + log.QuestId + ".asset", "logEntries." + log.Id + ".text");
                        }
            if (dialogueLog != dialogueLogs.Length) throw new FormatException("A dialogue log has no preservation source.");
            // Include objective override facts only when an actual legacy action writes them.
            foreach (var quest in result.Quests)
                foreach (var stage in quest.Stages)
                    foreach (var objective in stage.Objectives)
                    {
                        string fact = ObjectiveFact(quest.Id, objective.Id);
                        if (translator.Facts.Any(f => f.Name == fact)) objective.Condition = "(" + objective.Condition + ") or $" + fact;
                    }
            result.Clues = LegacyClueWriter.Prepare(resolve);
            CheckExternalReferences(result, dialogue, resolve);
            return result;

            void Field(string path, string value, string file, string destination)
            { if (value != null) dialogue.Manifest.AddField(path, value, file, destination); }
            List<string> Actions(LegacyQuestActionDto[] actions, string owner, string path, string asset, Quest quest)
            {
                var output = new List<string>();
                for (int i = 0; i < actions.Length; i++)
                {
                    int logs = translator.Logs.Count;
                    output.AddRange(translator.TranslateQuestAction(actions[i], owner).Commands);
                    if (translator.Logs.Count != logs)
                    {
                        var log = translator.Logs.Last();
                        Field(path + "[" + i + "]/logText", actions[i].LogText, asset, "logEntries." + log.Id + ".text");
                    }
                }
                return output;
            }
            void ConvertObjectives(LegacyObjectiveDto[] source, LegacyMarkerDto[] markers, Stage stage, int index, string path, string asset)
            {
                string prefix = index == 0 ? "since_accept" : "since_stage";
                for (int i = 0; i < source.Length; i++)
                {
                    var row = source[i]; var objective = new Objective { Id = Id(row.Id), Text = row.Title + "\n" + row.Description };
                    stage.Objectives.Add(objective);
                    string field = "stages[" + index + "].objectives[" + i + "]";
                    Field(path + "/objectives[" + i + "]/title", row.Title, asset, field + ".text");
                    Field(path + "/objectives[" + i + "]/description", row.Description, asset, field + ".text");
                    string counter = null;
                    switch (row.ObjectiveType)
                    {
                        case "collect_item": Require<ItemDefinition>(resolve, ItemId(row.TargetId));
                            if (row.TargetCount < 1) throw new FormatException("Non-positive item target: " + path);
                            objective.Condition = "has_item(" + Quote(ItemId(row.TargetId)) + ", " + row.TargetCount.ToString(CultureInfo.InvariantCulture) + ")";
                            objective.Progress = "item_count(" + Quote(ItemId(row.TargetId)) + ")"; objective.Target = row.TargetCount; objective.Latch = false; break;
                        case "custom_condition": objective.Condition = translator.StructuredCondition(row.Condition); break;
                        case "complete_encounter": counter = string.IsNullOrEmpty(row.TargetId) ? "sys_beats_completed" : BeatId(row.TargetId); break;
                        case "reach_system":
                            if (!string.IsNullOrEmpty(row.TargetId)) Require<AuthoredStarSystemDefinition>(resolve, Map(SystemIds, row.TargetId));
                            counter = string.IsNullOrEmpty(row.TargetId) ? "sys_star_systems_entered" : "visited_" + Map(SystemIds, row.TargetId); break;
                        case "interact_entity": Require<BodyTemplateDefinition>(resolve, BodyId(row.TargetId)); counter = "interacted_" + BodyId(row.TargetId); break;
                        case "destroy_entity": Require<EncounterDefinition>(resolve, Map(EncounterIds, row.TargetId)); counter = "defeated_" + Map(EncounterIds, row.TargetId); break;
                        case "visit_faction_territory": counter = "sys_territories_entered"; break;
                        default: throw new FormatException("Unmapped objective: " + row.ObjectiveType);
                    }
                    if (counter != null) objective.Condition = prefix + "(" + Quote(counter) + ") >= " + Math.Max(1, row.TargetCount);
                }
                for (int i = 0; i < markers.Length; i++)
                {
                    var marker = markers[i];
                    int target = Array.FindIndex(source, row => row.TargetId == (marker.EntityId ?? marker.SystemId));
                    if (target < 0) target = Math.Min(i, stage.Objectives.Count - 1);
                    if (target < 0) throw new FormatException("Marker has no objective: " + path);
                    var destination = stage.Objectives[target].Marker; destination.label = marker.Label;
                    if (!string.IsNullOrEmpty(marker.SystemId)) destination.starSystem = Require<AuthoredStarSystemDefinition>(resolve, Map(SystemIds, marker.SystemId));
                    else if (EncounterIds.ContainsKey(marker.EntityId))
                    { Require<EncounterDefinition>(resolve, Map(EncounterIds, marker.EntityId)); report.Warning("MIG-W092", path, "markers[" + i + "]", "Encounter marker label preserved; QuestMarker has no dynamic encounter target."); }
                    else { BodyId(marker.EntityId); destination.starSystem = Require<AuthoredStarSystemDefinition>(resolve, "starsys_ark_start"); destination.bodyTag = BodyTag(marker.EntityId); }
                    Field(path + "/markers[" + i + "]/label", marker.Label, asset, "stages[" + index + "].objectives[" + target + "].marker.label");
                }
            }
            void EntityOption(LegacyQuestDto quest, LegacyEntityOptionDto row, int index)
            {
                string shortId = QuestId(quest.Id).Substring(6), id = "beat_" + shortId + "_" + Id(row.Id), title = id.Substring(5) + "_start";
                BodyId(row.EntityId);
                var beat = new Beat { Id = id, Trigger = BeatTrigger.Interact, Salience = 30, Tag = BodyTag(row.EntityId), Node = title,
                    Condition = LegacyActionTranslator.And(translator.StructuredCondition(row.ShowCondition), translator.StructuredCondition(row.EnableCondition)) };
                foreach (var outcome in row.Outcomes) beat.OnStart.AddRange(translator.TranslateOutcome(outcome).Commands);
                beat.Condition = LegacyActionTranslator.And(beat.Condition, LegacyActionTranslator.ImplicitCosts(beat.OnStart));
                string path = dialogue.Root + "/Dialog/Legacy/" + id.Substring(5) + ".yarn";
                dialogue.TextFiles.Add(path, "title: " + title + "\ntags: \n---\n" + YarnEmitter.Escape(row.Text) + " #line:" + title + "_00\n-> Leave #leave #line:" + title + "_leave\n    <<abort_beat>>\n    <<stop>>\n===\n\n");
                Field(quest.SourcePath + "/entityOptions[" + index + "]/text", row.Text, path, title + "_00");
                dialogue.Manifest.AddGenerated("generated:" + quest.SourcePath + "/entityOptions[" + index + "]/leave", "Leave", path, title + "_leave");
                result.Beats.Add(beat);
            }
        }
        static string BodyId(string legacy)
        {
            if (!LegacyIdMap.Default.TryMap(LegacyIdKind.Body, legacy, out string body) || body == null) throw new FormatException("Unmapped body: " + legacy);
            return body;
        }
        internal static T Require<T>(Func<string, ContentDefinition> resolve, string id) where T : ContentDefinition =>
            resolve(id) is T value ? value : throw new FormatException("Missing real " + typeof(T).Name + ": " + id);
        static void CheckExternalReferences(Plan plan, LegacyNarrativeConverter.Plan dialogue, Func<string, ContentDefinition> resolve)
        {
            // Reuse the existing parser/registry metadata to check every ref literal before any write.
            var owned = new Dictionary<string, RefKind>(StringComparer.Ordinal);
            foreach (var quest in plan.Quests) owned.Add(quest.Id, RefKind.Quest);
            foreach (string beat in dialogue.Encounters.Select(e => e.BeatId).Concat(plan.Beats.Select(b => b.Id)).Distinct(StringComparer.Ordinal)) owned.Add(beat, RefKind.Beat);
            foreach (var clue in LegacyClueWriter.Texts) owned.Add(clue.Id, RefKind.Clue);
            foreach (var mystery in LegacyClueWriter.Mysteries) owned.Add(mystery.Id, RefKind.Mystery);
            var commands = plan.Quests.SelectMany(q => q.Accepted.Concat(q.Completed).Concat(q.Failed).Concat(q.Stages.SelectMany(s => s.OnComplete)))
                .Concat(plan.Beats.SelectMany(b => b.OnStart));
            foreach (string command in commands)
            {
                var ast = Game.Data.Logic.Compiler.LogicParser.ParseCommand(command);
                new LogicFunctionRegistry().TryGet(ast.Name, out var info);
                if (info == null) throw new FormatException("Unknown converted command: " + command);
                for (int i = 0; i < ast.Arguments.Count; i++)
                {
                    if (info.Params[i].ArgKind != LogicArgKind.Ref) continue;
                    string id = (ast.Arguments[i] as Game.Data.Logic.Compiler.LogicAstLiteral)?.Value as string;
                    if (id == "owner" && info.Params[i].RefKind == RefKind.Faction) continue;
                    if (id != null && owned.TryGetValue(id, out var kind))
                    {
                        if (kind != info.Params[i].RefKind) throw new FormatException("Wrong converted reference kind: " + id);
                        continue;
                    }
                    ContentDefinition value = resolve(id);
                    bool valid = info.Params[i].RefKind switch {
                        RefKind.Item => value is ItemDefinition, RefKind.Encounter => value is EncounterDefinition,
                        RefKind.StarSystem => value is AuthoredStarSystemDefinition, RefKind.Blueprint => value is ShipBlueprintDefinition,
                        RefKind.AudioCue => value is AudioCueDefinition, RefKind.Quest => value is QuestDefinition,
                        RefKind.Beat => value is StoryBeatDefinition, RefKind.Clue => value is ClueDefinition, _ => false };
                    if (!valid) throw new FormatException("Missing real command reference: " + id);
                }
            }
        }
        internal static void Strings(SerializedProperty array, IEnumerable<string> values)
        { var rows = values.ToArray(); array.arraySize = rows.Length; for (int i = 0; i < rows.Length; i++) array.GetArrayElementAtIndex(i).stringValue = rows[i]; }
        internal static void Pending(SerializedProperty wrapper, string source)
        {
            var field = wrapper.FindPropertyRelative("source");
            if (field.stringValue == source) return; // Preserve a valid compiled graph on an unchanged import.
            field.stringValue = source;
            wrapper.FindPropertyRelative("compilerVersion").intValue = 0;
            wrapper.FindPropertyRelative("hasErrors").boolValue = !string.IsNullOrWhiteSpace(source);
        }
        static void WriteQuest(LegacyAssetWriter writer, Quest quest)
        {
            writer.CreateOrUpdate<QuestDefinition>("Quests", quest.Id, so => {
                so.FindProperty("title").stringValue = quest.Title; so.FindProperty("shortDescription").stringValue = quest.Short;
                so.FindProperty("description").stringValue = quest.Description; so.FindProperty("kind").intValue = (int)quest.Kind; so.FindProperty("autoTrack").boolValue = quest.AutoTrack;
                var stages = so.FindProperty("stages"); stages.arraySize = quest.Stages.Count;
                for (int s = 0; s < stages.arraySize; s++)
                {
                    var input = quest.Stages[s]; var stage = stages.GetArrayElementAtIndex(s);
                    stage.FindPropertyRelative("id").stringValue = input.Id; stage.FindPropertyRelative("title").stringValue = input.Title;
                    var objectives = stage.FindPropertyRelative("objectives"); objectives.arraySize = input.Objectives.Count;
                    for (int o = 0; o < objectives.arraySize; o++)
                    {
                        var source = input.Objectives[o]; var output = objectives.GetArrayElementAtIndex(o);
                        output.FindPropertyRelative("id").stringValue = source.Id; output.FindPropertyRelative("text").stringValue = source.Text;
                        Pending(output.FindPropertyRelative("doneWhen"), source.Condition); Pending(output.FindPropertyRelative("progress"), source.Progress);
                        output.FindPropertyRelative("target").intValue = source.Target; output.FindPropertyRelative("latch").boolValue = source.Latch;
                        output.FindPropertyRelative("marker.starSystem").objectReferenceValue = source.Marker.starSystem;
                        output.FindPropertyRelative("marker.bodyTag").stringValue = source.Marker.bodyTag ?? "";
                        output.FindPropertyRelative("marker.label").stringValue = source.Marker.label ?? "";
                    }
                    Effects(stage.FindPropertyRelative("onComplete"), input.OnComplete);
                }
                Effects(so.FindProperty("onAccepted"), quest.Accepted); Effects(so.FindProperty("onCompleted"), quest.Completed); Effects(so.FindProperty("onFailed"), quest.Failed);
                var logs = so.FindProperty("logEntries"); logs.arraySize = quest.Logs.Count;
                for (int i = 0; i < logs.arraySize; i++) { logs.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue = quest.Logs[i].Id; logs.GetArrayElementAtIndex(i).FindPropertyRelative("text").stringValue = quest.Logs[i].Text; }
            }, out _);
        }
        static void WriteBeat(LegacyAssetWriter writer, Beat beat)
        {
            writer.CreateOrUpdate<StoryBeatDefinition>("Beats", beat.Id, so => {
                so.FindProperty("trigger").intValue = (int)beat.Trigger; so.FindProperty("firstVisitOnly").boolValue = beat.FirstVisit;
                so.FindProperty("where.bodyTags").arraySize = beat.Tag.Length == 0 ? 0 : 1;
                if (beat.Tag.Length != 0) so.FindProperty("where.bodyTags").GetArrayElementAtIndex(0).stringValue = beat.Tag;
                Pending(so.FindProperty("condition"), beat.Condition); so.FindProperty("salience").intValue = beat.Salience;
                so.FindProperty("weight").floatValue = 1; so.FindProperty("repeat").intValue = (int)beat.Repeat;
                so.FindProperty("yarnNode").stringValue = beat.Node; Effects(so.FindProperty("onStart"), beat.OnStart);
            }, out _);
        }
        static void Effects(SerializedProperty wrapper, IEnumerable<string> values)
        {
            var rows = values.ToArray();
            var source = wrapper.FindPropertyRelative("source");
            bool unchanged = source.arraySize == rows.Length;
            for (int i = 0; unchanged && i < rows.Length; i++)
                unchanged = source.GetArrayElementAtIndex(i).stringValue == rows[i];
            if (unchanged) return;
            Strings(source, rows);
            wrapper.FindPropertyRelative("compilerVersion").intValue = 0;
            wrapper.FindPropertyRelative("hasErrors").boolValue = rows.Length != 0;
        }
    }
}
