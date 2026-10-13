using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Validation;
using static Game.Editor.Narrative.NarrativeValidationContext;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-42 QST-V001/002/003/010/012: node roots, location vocabulary, reachability and shadowing.</summary>
    public sealed class BeatValidatorRules : INarrativeRuleProvider
    {
        public int Order => 15;
        public void Run(NarrativeValidationContext ctx, ValidationReport report)
        {
            var programs = ctx.Content.All<DialogProgramDefinition>(); var nodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var program in programs) if (program.Nodes != null) foreach (var node in program.Nodes) if (!string.IsNullOrEmpty(node.Title)) nodes.Add(node.Title);
            var roots = new HashSet<string>(StringComparer.Ordinal); var tags = QuestRuleTraversal.Tags(ctx);
            var beats = ctx.Content.All<StoryBeatDefinition>();
            foreach (var beat in beats)
            {
                string path = PathOf(beat); QuestDefinitionValidation.Beat(beat, report.For(path));
                if (!string.IsNullOrWhiteSpace(beat.YarnNode))
                {
                    if (!nodes.Contains(beat.YarnNode)) report.Error("QST-V001", path, "yarnNode", "Beat dialog node '" + beat.YarnNode + "' is not in the dialog program.");
                    else roots.Add(beat.YarnNode);
                }
                if (beat.Where?.bodyTags != null)
                    for (int i = 0; i < beat.Where.bodyTags.Length; i++) QuestRuleTraversal.Tag(beat.Where.bodyTags[i], "where.bodyTags[" + i + "]", path, tags, report, false);
            }
            foreach (var quest in ctx.Content.All<QuestDefinition>())
                if (quest.Stages != null) for (int i = 0; i < quest.Stages.Count; i++)
                {
                    var objectives = quest.Stages[i]?.objectives; if (objectives == null) continue;
                    for (int j = 0; j < objectives.Count; j++) QuestRuleTraversal.Tag(objectives[j]?.marker?.bodyTag,
                        "stages[" + i + "].objectives[" + j + "].marker.bodyTag", PathOf(quest), tags, report);
                }
            foreach (var rumor in ctx.Content.All<RumorDefinition>()) QuestRuleTraversal.Tag(rumor.TargetBodyTag, "targetBodyTag", PathOf(rumor), tags, report);
            var reached = new HashSet<string>(roots, StringComparer.Ordinal); var pending = new Queue<string>(roots.OrderBy(title => title, StringComparer.Ordinal));
            while (pending.Count > 0)
            {
                string title = pending.Dequeue();
                foreach (var program in programs)
                    if (program.Nodes != null && program.Code != null && program.Strings != null)
                        foreach (var node in program.Nodes)
                        {
                            if (node.Title != title) continue;
                            long end = Math.Min((long)program.Code.Count, (long)node.Start + node.Length);
                            for (int pc = Math.Max(0, node.Start); pc < end; pc++)
                            {
                                var instruction = program.Code[pc];
                                if ((instruction.Op != DialogOp.Jump && instruction.Op != DialogOp.Detour) || instruction.A < 0 || instruction.A >= program.Strings.Count) continue;
                                string target = program.Strings[instruction.A];
                                if (target != null && nodes.Contains(target) && reached.Add(target)) pending.Enqueue(target);
                            }
                        }
            }
            foreach (var program in programs) if (program.Nodes != null) for (int i = 0; i < program.Nodes.Count; i++)
                if (!reached.Contains(program.Nodes[i].Title ?? "")) report.Warning("QST-V010", PathOf(program), "nodes[" + i + "]", "Dialog node is unreachable from every beat root.");
            for (int i = 0; i < beats.Count; i++)
            {
                var a = beats[i]; if (a.Debug || !string.IsNullOrWhiteSpace(a.Condition?.Source)) continue;
                for (int j = i + 1; j < beats.Count; j++)
                {
                    var b = beats[j];
                    if (!b.Debug && string.IsNullOrWhiteSpace(b.Condition?.Source) && a.Trigger == b.Trigger && a.Salience == b.Salience && SameLocation(a.Where, b.Where))
                        report.Warning("QST-V012", PathOf(b), "salience", "Unconditional beat shares trigger, location and salience with '" + a.IdString + "'; verify intended weights.");
                }
            }
        }
        static bool SameLocation(BeatLocation a, BeatLocation b)
        {
            if (a?.starSystem != b?.starSystem || a?.hailEncounter != b?.hailEncounter) return false;
            var left = new HashSet<string>(a?.bodyTags ?? Array.Empty<string>(), StringComparer.Ordinal);
            return left.SetEquals(b?.bodyTags ?? Array.Empty<string>());
        }
    }
}
