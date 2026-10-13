using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Data.Definitions;
using Game.Data.Validation;
using static Game.Editor.Migration.Narrative.LegacyNarrativeMapping;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>Table-driven legacy logic lowering. No asset writes or prototype runtime dependencies.</summary>
    internal sealed class LegacyActionTranslator
    {
        readonly List<LegacyEncounterDto> sourceOrder = new List<LegacyEncounterDto>();
        readonly Dictionary<string, LegacyEncounterDto> encounters = new Dictionary<string, LegacyEncounterDto>(StringComparer.Ordinal);
        readonly SortedDictionary<string, FactDeclaration> facts = new SortedDictionary<string, FactDeclaration>(StringComparer.Ordinal);
        readonly Dictionary<string, int> logNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<ConvertedEncounter.LogEntry> logs = new List<ConvertedEncounter.LogEntry>();
        public IReadOnlyCollection<FactDeclaration> Facts => facts.Values;
        public IReadOnlyList<ConvertedEncounter.LogEntry> Logs => logs;
        internal void DeclareFact(FactDeclaration declaration)
        {
            if (facts.TryGetValue(declaration.Name, out var existing) && existing.Kind != declaration.Kind)
                throw new FormatException("Conflicting converted fact kind: " + declaration.Name);
            facts[declaration.Name] = declaration;
        }

        public LegacyActionTranslator(LegacyNarrativeLoader.Sources sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            foreach (var row in Flags.Values) if (row.Fact.HasValue) facts[row.Fact.Value.Name] = row.Fact.Value;
            foreach (var encounter in sources.Encounters)
            {
                Id(encounter.Id);
                if (encounters.ContainsKey(encounter.Id)) throw new FormatException(encounter.SourcePath + ": duplicate encounter ID " + encounter.Id);
                encounters.Add(encounter.Id, encounter); sourceOrder.Add(encounter);
                var pages = new HashSet<string>(StringComparer.Ordinal);
                foreach (var page in encounter.Pages)
                    if (!pages.Add(Id(page.Id))) throw new FormatException(encounter.SourcePath + ": duplicate page ID " + page.Id);
            }
        }

        public List<ConvertedEncounter> ConvertAll(ValidationReport report)
        {
            int errors = report.ErrorCount;
            var result = new List<ConvertedEncounter>();
            // Source insertion order is the export order, never Dictionary enumeration order.
            foreach (var encounter in sourceOrder)
            {
                try
                {
                    var converted = new ConvertedEncounter { BeatId = BeatId(encounter.Id), Source = encounter };
                    for (int p = 0; p < encounter.Pages.Length; p++)
                    {
                        var page = encounter.Pages[p];
                        var node = new ConvertedEncounter.Node { Title = NodeTitle(encounter.Id, page.Id), Tags = page.Tags ?? Array.Empty<string>(), Source = page };
                        if (!string.IsNullOrEmpty(page.Audio)) node.Statements.Add("<<play_sfx " + Map(AudioIds, page.Audio) + ">>");
                        for (int o = 0; o < page.Options.Length; o++)
                        {
                            try { node.Options.Add(TranslateOption(encounter, page, page.Options[o])); }
                            catch (FormatException e) { Error(report, encounter.SourcePath, "pages[" + p + "].options[" + o + "]", e.Message); }
                            catch (OverflowException e) { Error(report, encounter.SourcePath, "pages[" + p + "].options[" + o + "]", e.Message); }
                        }
                        converted.Nodes.Add(node);
                    }
                    result.Add(converted);
                }
                catch (FormatException e) { Error(report, encounter.SourcePath, "pages", e.Message); }
            }
            return report.ErrorCount == errors ? result : null;
        }
        public ConvertedEncounter.Option TranslateOption(LegacyEncounterDto encounter, LegacyPageDto page, LegacyOptionDto source)
        {
            var option = new ConvertedEncounter.Option { Source = source };
            foreach (string action in SplitActions(source.Actions))
            {
                if (option.Terminal || option.Unavailable) break;
                TranslateAction(action, encounter, page, option);
            }
            if (option.Unavailable) return option;
            option.ShowCondition = Requirement(source.ShowReq);
            option.EnableCondition = And(Requirement(source.EnableReq), ImplicitCosts(option.Commands));
            if (option.ShowCondition.Length != 0) option.Conditions.Add(option.ShowCondition);
            if (option.EnableCondition.Length != 0) option.Conditions.Add(option.EnableCondition);
            return option;
        }

        public string Requirement(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "";
            Parse(source, out string name, out string body);
            string[] args = Arguments(body);
            switch (Map(RequirementFunctions, name))
            {
                case "credits": Arity(args, 1); return "credits() >= " + Number(args[0]);
                case "has_item": Arity(args, 2); return "has_item(" + Quote(ItemId(args[0])) + ", " + Number(args[1]) + ")";
                case "flag": Arity(args, 2); return FlagCondition(args[0]);
                case "quest_active": Arity(args, 1); return "quest_active(" + Quote(QuestId(args[0])) + ")";
                case "quest_state": Arity(args, 2); return Map(QuestStateFunctions, args[1]) + "(" + Quote(QuestId(args[0])) + ")";
                default: throw new FormatException("Unmapped requirement: " + name);
            }
        }

        public ConvertedEncounter.Option TranslateQuestAction(LegacyQuestActionDto action, string owningQuest)
        {
            var output = new ConvertedEncounter.Option();
            string operation = Map(QuestCommands, action.ActionType);
            string quest = action.QuestId ?? owningQuest;
            switch (operation)
            {
                case "log": AddLog(output, quest, action.LogText); break;
                case "start_quest": case "track_quest": case "fail_quest": Command(output, operation + " " + QuestId(quest)); break;
                case "template": Command(output, "start_quest " + QuestId(action.TemplateId)); break;
                case "flag": WriteFlag(output, action.Key, action.Value); break;
                case "spawn_encounter":
                    int count = 1;
                    foreach (var parameter in action.Parameters ?? Array.Empty<LegacyParameterDto>())
                        if (parameter.Key == "count") count = Number(parameter.Value);
                        else throw new FormatException("Unmapped spawn parameter: " + parameter.Key);
                    Command(output, "spawn_encounter " + Map(EncounterIds, action.EntityId) + " player " + count); break;
                case "adjust_relation": Command(output, "adjust_relation owner " + action.IntValue); break;
                case "queue_beat": Command(output, "queue_beat " + BeatId(action.EncounterId)); break;
                case "objective": Objective(output, quest, action.ObjectiveId); break;
                default: throw new FormatException("Unmapped quest action: " + action.ActionType);
            }
            return output;
        }

        public ConvertedEncounter.Option TranslateOutcome(LegacyOutcomeDto outcome)
        {
            string operation = Map(OutcomeActions, outcome.Type);
            var result = new ConvertedEncounter.Option();
            if (operation == "REMOVE_ITEM")
                Command(result, "take_item " + ItemId(outcome.Parameter1) + " " + Number(outcome.Parameter2));
            else Custom(outcome.Parameter1, Parameters(outcome.Parameter2), result);
            result.EnableCondition = ImplicitCosts(result.Commands);
            if (result.EnableCondition.Length != 0) result.Conditions.Add(result.EnableCondition);
            return result;
        }

        // These are dialog/quest predicates. Trigger event-value tests stay in source metadata for NAR-016,
        // which owns event matching, firstVisitOnly and the two explicitly dropped triggers.
        public string StructuredCondition(LegacyConditionDto source)
        {
            if (source == null) return "";
            if (source.All != null)
            {
                string condition = "";
                foreach (var child in source.All) condition = And(condition, StructuredCondition(child));
                return condition;
            }
            switch (source.Type)
            {
                case "world_state": return FlagCondition(source.Key);
                case "quest_state": return Map(QuestStateFunctions, source.Value) + "(" + Quote(QuestId(source.QuestId)) + ")";
                case "objective_state":
                    string predicate = "objective_done(" + Quote(QuestId(source.QuestId)) + ", " + Quote(Id(source.ObjectiveId)) + ")";
                    return Bool(source.Value) ? predicate : "not " + predicate;
                case "faction_relation":
                    string comparison = source.Comparison == "greater_or_equal" ? ">=" : source.Comparison == "less" ? "<" :
                        source.Comparison == "less_or_equal" ? "<=" : throw new FormatException("Unmapped relation comparison: " + source.Comparison);
                    return "relation(\"owner\") " + comparison + " " + source.IntValue;
                default: throw new FormatException("Unmapped predicate: " + source.Type);
            }
        }

        void TranslateAction(string source, LegacyEncounterDto encounter, LegacyPageDto page, ConvertedEncounter.Option output)
        {
            Parse(source, out string name, out string body);
            string operation = Map(ActionCommands, name);
            if (operation == "custom")
            {
                int separator = body.IndexOf(',');
                string custom = (separator < 0 ? body : body.Substring(0, separator)).Trim();
                Custom(custom, Parameters(separator < 0 ? "" : body.Substring(separator + 1)), output);
                return;
            }
            string[] args = Arguments(body);
            string current = NodeTitle(encounter.Id, page.Id);
            switch (operation)
            {
                case "jump": Arity(args, 1); Flow(output, "jump " + Target(encounter, args[0])); break;
                case "detour": Arity(args, 1); Flow(output, "detour " + Target(encounter, args[0])); Flow(output, "jump " + current); break;
                case "return": Arity(args, 0); Flow(output, "return"); break;
                case "stop": Arity(args, 1); if (!Bool(args[0])) Command(output, "abort_beat"); Flow(output, "stop"); break;
                case "random": Arity(args, 1); RandomBranch(output, encounter, args[0]); break;
                case "flag": Arity(args, 2); WriteFlag(output, args[0], args[1]); break;
                case "give_item":
                    Arity(args, 2); int count = Number(args[1]);
                    if (args[0].StartsWith("ship_", StringComparison.Ordinal)) Command(output, "give_ship " + Id(args[0]));
                    else if (args[0] == "500" && encounter.Id == "event_derelict_salvage" && page.Id == "module_found" && output.Source.Text == "Take Module")
                        Command(output, "give_loot loot_salvage_module " + count);
                    else if (args[0] == "500" && encounter.Id == "event_distress_trap" && page.Id == "real_survivors")
                        Command(output, "give_item item_research_blueprint " + count);
                    else Command(output, "give_item " + ItemId(args[0]) + " " + count);
                    break;
                case "take_item": Arity(args, 2); Command(output, "take_item " + ItemId(args[0]) + " " + Number(args[1])); break;
                case "credits": Arity(args, 1); Signed(output, Number(args[0]), "give_credits", "take_credits"); break;
                case "fuel": Arity(args, 1); Signed(output, Number(args[0]), "give_item resource_fuel", "take_item resource_fuel"); break;
                case "crew": Arity(args, 1); Signed(output, Number(args[0]), "give_item resource_crew", "take_item resource_crew"); break;
                case "hull": Arity(args, 1); Signed(output, Number(args[0]), "repair_ship", "damage_ship"); break;
                case "add_fact": Arity(args, 1); Command(output, "add_fact research_points " + Number(args[0])); break;
                case "give_loot": Arity(args, 1); Command(output, "give_loot " + Map(LootIds, encounter.LootTable) + " " + Number(args[0])); break;
                case "remove_body": Arity(args, 0); Command(output, "remove_body"); break;
                case "start_combat": Arity(args, 1); Command(output, "start_combat " + Map(EncounterIds, args[0])); output.Terminal = true; break;
                case "open_merchant": Arity(args, 0); Command(output, "open_merchant " + Map(MerchantIds, encounter.Id)); break;
                case "repair_ship": Arity(args, 0); Command(output, "repair_ship 100"); break;
                case "open_ship_editor": Arity(args, 0); Command(output, "open_ship_editor"); output.Terminal = true; break;
                case "play_sfx": Arity(args, 1); Command(output, "play_sfx " + Map(AudioIds, args[0])); break;
                case "nested":
                    Arity(args, 1);
                    if (!encounters.TryGetValue(args[0], out var nested) || nested.Pages.Length == 0) throw new FormatException("Missing nested encounter start: " + args[0]);
                    Flow(output, "detour " + Target(nested, nested.Pages[0].Id)); Flow(output, "jump " + current); break;
                case "unavailable":
                    Arity(args, 1); output.Statements.Clear(); output.Commands.Clear(); output.Conditions.Clear(); output.Unavailable = true;
                    output.Statements.Add("// LEGACY-UNAVAILABLE PLAY_FROM_POOL(" + args[0] + "): -> " + output.Source.Text); break;
                default: throw new FormatException("Unmapped action: " + name);
            }
        }

        void Custom(string name, Dictionary<string, string> args, ConvertedEncounter.Option output)
        {
            string[] allowed = Map(CustomParameterKeys, name).Split(',');
            foreach (var argument in args)
            {
                if (Array.IndexOf(allowed, argument.Key) < 0) throw new FormatException("Unmapped parameter for " + name + ": " + argument.Key);
                if (argument.Key == "track" || argument.Key == "intro") Bool(argument.Value);
            }
            switch (Map(CustomCommands, name))
            {
                case "flag": WriteFlag(output, Required(args, "key"), Required(args, "value")); break;
                case "start_quest": case "complete_quest": case "fail_quest":
                    Command(output, Map(CustomCommands, name) + " " + QuestId(Required(args, "questId"))); break;
                case "template": Command(output, "start_quest " + QuestId(Required(args, "templateId"))); break;
                case "objective": Objective(output, Required(args, "questId"), Required(args, "objectiveId")); break;
                case "log": AddLog(output, Required(args, "questId"), Required(args, "text")); break;
                case "spawn_encounter": Command(output, "spawn_encounter " + Map(EncounterIds, Required(args, "entityId")) + " " + BodyTag(Required(args, "anchor")) + " " + Number(Required(args, "count"))); break;
                case "queue_beat": Command(output, "queue_beat " + BeatId(Required(args, "id"))); break;
                case "gate":
                    if (Required(args, "system") != "ark_start" || Required(args, "from") != "exitgate_dormant" || Required(args, "to") != "exitgate_active" || !Bool(Required(args, "inPlace")))
                        throw new FormatException("Unmapped world.replace_entity target.");
                    Command(output, "set_fact gate_ark_start_active true"); break;
                case "request_lane_jump": Command(output, "request_lane_jump starsys_whisper"); output.Terminal = true; break;
                default: throw new FormatException("Unmapped custom action: " + name);
            }
        }

        void Objective(ConvertedEncounter.Option output, string quest, string objective)
        {
            string fact = ObjectiveFact(quest, objective);
            facts[fact] = Declaration(fact, Game.Data.Logic.FactKind.Bool);
            Command(output, "set_fact " + fact + " true");
        }
        void AddLog(ConvertedEncounter.Option output, string quest, string text)
        {
            quest = QuestId(quest);
            if (text == null) throw new FormatException("Missing log text.");
            logNumbers.TryGetValue(quest, out int number); logNumbers[quest] = ++number;
            string id = "legacy_" + number;
            logs.Add(new ConvertedEncounter.LogEntry(quest, id, text)); Command(output, "quest_log " + quest + " " + id);
        }
        static void WriteFlag(ConvertedEncounter.Option output, string key, string value)
        {
            if (key != null && Flags.TryGetValue(key, out var row))
            {
                if (SymbolicFlagValues.TryGetValue(key, out string expected))
                { if (value != expected) throw new FormatException("Unmapped value for legacy flag: " + key + " = " + value); }
                else if (!Bool(value)) throw new FormatException("Unmapped false grant for legacy flag: " + key);
                if (row.Commands.Count == 0) throw new FormatException("Legacy content cannot write code-owned flag: " + key);
                foreach (string command in row.Commands) Command(output, command);
            }
            else if (!DroppedQuestFlag(key, out _, out _)) throw new FormatException("Unmapped legacy flag: " + key);
            // Dropped completion flags have an explicit map; the quest command/objective owns that state.
        }
        static string FlagCondition(string key)
        {
            if (key != null && Flags.TryGetValue(key, out var row)) return row.Condition;
            if (DroppedQuestFlag(key, out string quest, out bool failed)) return (failed ? "quest_failed(" : "quest_completed(") + Quote(quest) + ")";
            throw new FormatException("Unmapped legacy flag: " + key);
        }
        static bool DroppedQuestFlag(string key, out string quest, out bool failed)
        {
            failed = key != null && key.EndsWith(".failed", StringComparison.Ordinal);
            quest = null;
            if (key != null && DroppedFlags.TryGetValue(key, out quest)) return true;
            if (key == null || !key.StartsWith("quest.", StringComparison.Ordinal) || !(failed || key.EndsWith(".complete", StringComparison.Ordinal))) return false;
            int suffix = key.LastIndexOf('.'); quest = QuestId("quest_" + key.Substring(6, suffix - 6)); return true;
        }
        static void Signed(ConvertedEncounter.Option output, int value, string positive, string negative)
        {
            if (value == 0) return;
            Command(output, (value > 0 ? positive : negative) + " " + Math.Abs(value));
        }
        static void Command(ConvertedEncounter.Option output, string command)
        { output.Commands.Add(command); output.Statements.Add("<<" + command + ">>"); }
        static void Flow(ConvertedEncounter.Option output, string flow)
        { output.HasFlow = true; output.Statements.Add("<<" + flow + ">>"); }
        static string Target(LegacyEncounterDto encounter, string page)
        {
            foreach (var row in encounter.Pages) if (row.Id == page) return NodeTitle(encounter.Id, page);
            throw new FormatException("Missing page target: " + encounter.Id + "/" + page);
        }
        static void RandomBranch(ConvertedEncounter.Option output, LegacyEncounterDto encounter, string body)
        {
            string[] branches = body.Split('|');
            if (branches.Length < 2) throw new FormatException("Random branch requires at least two destinations.");
            var weights = new int[branches.Length]; var targets = new string[branches.Length]; long total = 0;
            for (int i = 0; i < branches.Length; i++)
            {
                int separator = branches[i].IndexOf(':');
                if (separator < 1) throw new FormatException("Invalid weighted branch: " + branches[i]);
                weights[i] = Number(branches[i].Substring(0, separator));
                if (weights[i] <= 0) throw new FormatException("Random branch weight must be positive.");
                total = checked(total + weights[i]); targets[i] = Target(encounter, branches[i].Substring(separator + 1).Trim());
            }
            long cumulative = 0;
            for (int i = 0; i < branches.Length; i++)
            {
                cumulative += weights[i];
                if (i == branches.Length - 1) Flow(output, "else");
                else
                {
                    int threshold = (int)Math.Round(100m * cumulative / total, MidpointRounding.AwayFromZero);
                    string condition = "roll(100) <= " + threshold; output.Conditions.Add(condition);
                    Flow(output, (i == 0 ? "if " : "elseif ") + condition);
                }
                Flow(output, "jump " + targets[i]);
            }
            Flow(output, "endif");
        }
        public static string ImplicitCosts(IReadOnlyList<string> commands)
        {
            string condition = "";
            foreach (string command in commands)
            {
                string[] words = command.Split(' ');
                if (words[0] == "take_item") condition = And(condition, "has_item(" + Quote(words[1]) + ", " + words[2] + ")");
                else if (words[0] == "take_credits") condition = And(condition, "credits() >= " + words[1]);
            }
            return condition;
        }
        public static string And(string left, string right) => string.IsNullOrEmpty(left) ? right ?? "" : string.IsNullOrEmpty(right) ? left : "(" + left + ") and (" + right + ")";
        static int Number(string value) => int.TryParse(value?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result)
            ? result : throw new FormatException("Expected Int32: " + value);
        static bool Bool(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" ? true :
            string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0" ? false : throw new FormatException("Expected legacy Bool: " + value);
        static void Arity(string[] args, int count) { if (args.Length != count) throw new FormatException("Expected " + count + " arguments; got " + args.Length + "."); }
        static string[] Arguments(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return Array.Empty<string>();
            string[] args = body.Split(','); for (int i = 0; i < args.Length; i++) args[i] = args[i].Trim(); return args;
        }
        static void Parse(string source, out string name, out string body)
        {
            source = source.Trim(); int start = source.IndexOf('(');
            if (start < 0) { name = source; body = ""; return; }
            if (!source.EndsWith(")", StringComparison.Ordinal)) throw new FormatException("Malformed legacy invocation: " + source);
            name = source.Substring(0, start).Trim(); body = source.Substring(start + 1, source.Length - start - 2);
        }
        static IEnumerable<string> SplitActions(string source)
        {
            if (source == null) throw new FormatException("Missing action list.");
            int depth = 0, start = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '(') depth++;
                else if (source[i] == ')') { if (--depth < 0) throw new FormatException("Unbalanced legacy action list."); }
                else if (source[i] == ';' && depth == 0)
                { string action = source.Substring(start, i - start).Trim(); if (action.Length != 0) yield return action; start = i + 1; }
            }
            if (depth != 0) throw new FormatException("Unbalanced legacy action list.");
            string tail = source.Substring(start).Trim(); if (tail.Length != 0) yield return tail;
        }
        static Dictionary<string, string> Parameters(string source)
        {
            var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(source)) return pairs;
            foreach (string pair in source.Split('|'))
            {
                int separator = pair.IndexOf('=');
                if (separator < 1) throw new FormatException("Malformed legacy parameter: " + pair);
                string key = pair.Substring(0, separator).Trim();
                if (pairs.ContainsKey(key)) throw new FormatException("Duplicate legacy parameter: " + key);
                pairs.Add(key, key == "text" ? pair.Substring(separator + 1) : pair.Substring(separator + 1).Trim());
            }
            return pairs;
        }
        static string Required(Dictionary<string, string> args, string key) => args.TryGetValue(key, out string value)
            ? value : throw new FormatException("Missing legacy parameter: " + key);
        static void Error(ValidationReport report, string path, string field, string message)
        {
            int separator = path.IndexOf(':');
            report.Error("MIG-E003", separator < 0 ? path : path.Substring(0, separator),
                separator < 0 ? field : path.Substring(separator + 1) + "." + field, message);
        }
    }
}
