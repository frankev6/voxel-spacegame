using System;
using Game.Data.Logic;
using Game.Narrative;

namespace Game.App.Services
{
    /// <summary>SPEC-40 §5.5: composed logic owners and a bounded, allocation-free scope stack.</summary>
    public sealed class LogicContext : ILogicContext, ILogicEffectHost
    {
        readonly FactStore facts;
        readonly Func<ulong> campaignSeed;
        readonly LogicScope[] scopes = new LogicScope[8];
        int depth;

        public LogicContext(FactStore facts, ILogicEconomy economy, ILogicNarrative narrative,
            ILogicWorld world, Func<ulong> campaignSeed)
        {
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            Narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
            World = world ?? throw new ArgumentNullException(nameof(world));
            this.campaignSeed = campaignSeed ?? throw new ArgumentNullException(nameof(campaignSeed));
        }
        public ILogicFacts Facts => facts;
        public ILogicEconomy Economy { get; }
        public ILogicNarrative Narrative { get; }
        public ILogicWorld World { get; }
        public ulong CampaignSeed => campaignSeed();
        public LogicScope Scope => depth == 0 ? default : scopes[depth - 1];
        public Action BlockingCommandCompleted { get; set; }

        public void PushScope(in LogicScope scope)
        {
            if (depth == scopes.Length) throw new InvalidOperationException("Logic scope depth exceeds eight.");
            scopes[depth++] = scope;
        }
        public void PopScope()
        {
            if (depth == 0) throw new InvalidOperationException("No logic scope to pop.");
            scopes[--depth] = default;
        }
        public void EffectRejected()
        {
            facts.Trace?.Add(new LogicTraceEntry
            {
                Kind = LogicTraceKind.EffectRejected,
                Source = FactWriteSource.Effect,
                Frame = UnityEngine.Time.frameCount,
                UniversalTime = World.UniversalTime
            });
        }
    }
}
