using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Narrative.Clues;
using Game.Narrative.Quests;
namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §5.5: tracked unfinished objectives, then known unresolved targeted rumors.</summary>
    internal static class NarrativeMarkers
    {
        internal static void Rebuild(QuestService quests, ClueService clues, IReadOnlyList<QuestDefinition> questDefinitions,
            IReadOnlyList<ClueDefinition> clueDefinitions, List<NarrativeMarker> into)
        {
            into.Clear(); int q = quests.TrackedQuestIndex, stage = quests.GetStage(q);
            if (q > 0 && q <= questDefinitions.Count && (uint)stage < (uint)questDefinitions[q - 1].Stages.Count)
                foreach (var objective in questDefinitions[q - 1].Stages[stage].objectives)
                {
                    var marker = objective.marker;
                    if (quests.IsObjectiveDone(q, objective.id) || marker?.starSystem == null) continue;
                    into.Add(new NarrativeMarker(NarrativeMarkerKind.Objective, marker.starSystem.Id, marker.bodyTag ?? "",
                        string.IsNullOrEmpty(marker.label) ? objective.text ?? "" : marker.label, q, 0));
                }
            int objectives = into.Count;
            for (int c = 1; c <= clueDefinitions.Count; c++)
            {
                if (clueDefinitions[c - 1] is not RumorDefinition rumor || rumor.Target == null || !clues.IsKnown(c) || clues.IsResolved(c)) continue;
                var view = new NarrativeMarker(NarrativeMarkerKind.Rumor, rumor.Target.Id, rumor.TargetBodyTag ?? "", rumor.Title ?? "", 0, c);
                int at = into.Count; into.Add(view);
                while (at > objectives && clues.OrdinalOf(into[at - 1].ClueIndex) > clues.OrdinalOf(c)) { into[at] = into[at - 1]; at--; }
                into[at] = view;
            }
        }
    }
}
