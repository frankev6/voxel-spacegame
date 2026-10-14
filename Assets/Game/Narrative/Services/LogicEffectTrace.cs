using Game.Data.Logic;

namespace Game.Narrative
{
    /// <summary>Existing developer trace at successful narrative effect dispatches.</summary>
    internal static class LogicEffectTrace
    {
        internal static void Applied(ILogicContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (context.Facts is not FactStore facts || facts.Trace == null) return;
            var scope = context.Scope;
            facts.Trace.Add(new LogicTraceEntry
            {
                Kind = LogicTraceKind.EffectApplied, Source = FactWriteSource.Effect,
                Frame = UnityEngine.Time.frameCount, UniversalTime = context.World.UniversalTime,
                Subject = scope.BeatIndex != 0 ? scope.BeatIndex : scope.QuestIndex,
                ContextId = (ulong)(uint)scope.QuestInstance
            });
#endif
        }
    }
}
