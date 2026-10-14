using System;
using System.Collections.Generic;

namespace Game.Data.Logic
{
    /// <summary>Canonical SPEC-40 vocabulary. Lookup copies mutable public metadata so callers cannot alter the table.</summary>
    public sealed class LogicFunctionRegistry
    {
        [Game.Core.StaticStateOk("built once from constant function table, never written")] static readonly Dictionary<string, LogicFunctionInfo> entries = Build();

        public bool TryGet(string name, out LogicFunctionInfo info)
        {
            if (name == null || !entries.TryGetValue(name, out var source)) { info = null; return false; }
            info = new LogicFunctionInfo {
                Name = source.Name, IsEffect = source.IsEffect, ReturnType = source.ReturnType,
                Params = (LogicParamInfo[])source.Params.Clone(), AllowedUsage = source.AllowedUsage,
                Flow = source.Flow, Milestone = source.Milestone
            };
            return true;
        }

        // Authoring export through the existing Editor friend assembly; every row/parameter array is copied.
        internal IReadOnlyList<LogicFunctionInfo> Snapshot()
        {
            var names = new List<string>(entries.Keys); names.Sort(StringComparer.Ordinal);
            var result = new LogicFunctionInfo[names.Count];
            for (int i = 0; i < names.Count; i++) { TryGet(names[i], out var info); result[i] = info; }
            return result;
        }

        internal string Suggest(string name)
        {
            string best = null; int distance = 3;
            foreach (string candidate in entries.Keys)
            {
                int d = Distance(name, candidate);
                if (d < distance || d == distance && d <= 2 && string.CompareOrdinal(candidate, best) < 0)
                { best = candidate; distance = d; }
            }
            return best;
        }

        internal static int Distance(string a, string b)
        {
            a = a ?? ""; b = b ?? "";
            var previous = new int[b.Length + 1]; var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                var swap = previous; previous = current; current = swap;
            }
            return previous[b.Length];
        }

        static LogicParamInfo Ref(string name, RefKind kind) =>
            new LogicParamInfo { Name = name, ArgKind = LogicArgKind.Ref, RefKind = kind };
        static LogicParamInfo Arg(string name, LogicArgKind kind, bool optional = false, int defaultInt = 0) =>
            new LogicParamInfo { Name = name, ArgKind = kind, Optional = optional, DefaultInt = defaultInt };
        static Dictionary<string, LogicFunctionInfo> Build()
        {
            var result = new Dictionary<string, LogicFunctionInfo>(StringComparer.Ordinal);
            void Condition(string name, LogicType type, LogicUsage usage, params LogicParamInfo[] args) =>
                result.Add(name, new LogicFunctionInfo { Name = name, ReturnType = type, AllowedUsage = usage,
                    Params = args, Milestone = 3 });
            void Effect(string name, LogicUsage usage, EffectFlow flow, params LogicParamInfo[] args) =>
                result.Add(name, new LogicFunctionInfo { Name = name, IsEffect = true, AllowedUsage = usage,
                    Flow = flow, Params = args, Milestone = name == "replace_body" ? 4 : 3 });
            const LogicUsage ac = LogicUsage.AnyCondition, ae = LogicUsage.AnyEffect, dialog = LogicUsage.DialogCommand;
            Condition("has_item", LogicType.Bool, ac, Ref("item", RefKind.Item), Arg("count", LogicArgKind.Int, true, 1));
            Condition("item_count", LogicType.Int, ac, Ref("item", RefKind.Item));
            Condition("credits", LogicType.Int, ac);
            foreach (string name in new[] { "quest_not_started", "quest_active", "quest_completed", "quest_failed" })
                Condition(name, LogicType.Bool, ac, Ref("quest", RefKind.Quest));
            Condition("quest_stage", LogicType.Int, ac, Ref("quest", RefKind.Quest));
            Condition("objective_done", LogicType.Bool, ac, Ref("quest", RefKind.Quest), Arg("objective", LogicArgKind.Text));
            Condition("clue_known", LogicType.Bool, ac, Ref("clue", RefKind.Clue));
            Condition("mystery_solved", LogicType.Bool, ac, Ref("mystery", RefKind.Mystery));
            Condition("beat_count", LogicType.Int, ac & ~LogicUsage.QuestObjective, Ref("beat", RefKind.Beat));
            Condition("since_accept", LogicType.Int, LogicUsage.QuestObjective, Arg("fact", LogicArgKind.Fact));
            Condition("since_stage", LogicType.Int, LogicUsage.QuestObjective, Arg("fact", LogicArgKind.Fact));
            Condition("in_star_system", LogicType.Bool, ac, Ref("system", RefKind.StarSystem));
            Condition("relation", LogicType.Int, ac, Ref("faction", RefKind.Faction));
            Condition("star_system_owned", LogicType.Bool, ac);
            Condition("game_hours", LogicType.Int, ac);
            Condition("roll", LogicType.Int, LogicUsage.DialogCondition, Arg("sides", LogicArgKind.Int));
            Condition("visited_count", LogicType.Int, LogicUsage.DialogCondition, Arg("node", LogicArgKind.Text));
            // set_fact's second parameter is refined against the selected fact kind by the type checker.
            Effect("set_fact", ae, EffectFlow.Immediate, Arg("fact", LogicArgKind.Fact), Arg("value", LogicArgKind.Int));
            Effect("add_fact", ae, EffectFlow.Immediate, Arg("fact", LogicArgKind.Fact), Arg("delta", LogicArgKind.Int));
            foreach (string name in new[] { "give_item", "take_item" })
                Effect(name, ae, EffectFlow.Immediate, Ref("item", RefKind.Item), Arg("count", LogicArgKind.Int, true, 1));
            foreach (string name in new[] { "give_credits", "take_credits" })
                Effect(name, ae, EffectFlow.Immediate, Arg("amount", LogicArgKind.Int));
            Effect("give_loot", ae, EffectFlow.Immediate, Ref("table", RefKind.LootTable), Arg("picks", LogicArgKind.Int, true, -1));
            Effect("give_ship", ae, EffectFlow.Immediate, Ref("blueprint", RefKind.Blueprint));
            foreach (string name in new[] { "start_quest", "fail_quest", "complete_quest", "track_quest" })
                Effect(name, ae, EffectFlow.Immediate, Ref("quest", RefKind.Quest));
            Effect("quest_log", ae, EffectFlow.Immediate, Ref("quest", RefKind.Quest), Arg("entry", LogicArgKind.Tag));
            Effect("reveal_clue", ae, EffectFlow.Immediate, Ref("clue", RefKind.Clue));
            Effect("queue_beat", ae, EffectFlow.Immediate, Ref("beat", RefKind.Beat));
            Effect("complete_beat", dialog, EffectFlow.Immediate, Ref("beat", RefKind.Beat));
            Effect("abort_beat", dialog, EffectFlow.Immediate);
            Effect("spawn_encounter", ae, EffectFlow.Immediate, Ref("encounter", RefKind.Encounter),
                Arg("anchor", LogicArgKind.Tag, true), Arg("count", LogicArgKind.Int, true, 1));
            Effect("start_combat", dialog, EffectFlow.Terminal, Ref("encounter", RefKind.Encounter));
            Effect("remove_body", ae, EffectFlow.Immediate);
            Effect("replace_body", ae, EffectFlow.Immediate, Ref("template", RefKind.Entity));
            Effect("request_lane_jump", dialog, EffectFlow.Terminal, Ref("system", RefKind.StarSystem));
            Effect("open_merchant", dialog, EffectFlow.Blocking, Ref("merchant", RefKind.Merchant));
            Effect("open_ship_editor", dialog, EffectFlow.Terminal);
            Effect("repair_ship", ae, EffectFlow.Immediate, Arg("percent", LogicArgKind.Int));
            Effect("damage_ship", ae, EffectFlow.Immediate, Arg("amount", LogicArgKind.Int));
            Effect("adjust_relation", ae, EffectFlow.Immediate, Ref("faction", RefKind.Faction), Arg("delta", LogicArgKind.Int));
            Effect("play_sfx", ae, EffectFlow.Immediate, Ref("cue", RefKind.AudioCue));
            Effect("notify", ae, EffectFlow.Immediate, Arg("text", LogicArgKind.Text));
            return result;
        }
    }
}
