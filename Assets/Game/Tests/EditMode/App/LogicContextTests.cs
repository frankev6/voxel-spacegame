using System;
using Game.App.Services;
using Game.Core.Events;
using Game.Data.Logic;
using Game.Narrative;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    public sealed class LogicContextTests
    {
        [Test] public void ScopeStack_DepthLimit()
        {
            using var environment = new LogicTestEnvironment();
            var facts = new FactStore(environment.Facts);
            var owners = new FakeLogicContext(environment.Facts) { UniversalTime = 23 };
            ulong seed = 7;
            var context = new LogicContext(facts, owners, owners, owners, () => seed);
            Assert.AreSame(facts, context.Facts); Assert.AreSame(owners, context.World);
            for (int i = 1; i <= 8; i++)
            {
                context.PushScope(new LogicScope(LogicUsage.QuestEffect, i, 0, 0, false, default));
                Assert.AreEqual(i, context.Scope.QuestIndex);
            }
            Assert.Throws<InvalidOperationException>(() => context.PushScope(default));
            Assert.AreEqual(8, context.Scope.QuestIndex);
            for (int i = 8; i > 0; i--) { Assert.AreEqual(i, context.Scope.QuestIndex); context.PopScope(); }
            Assert.AreEqual(LogicUsage.None, context.Scope.Usage);
            Assert.Throws<InvalidOperationException>(() => context.PopScope());
            seed = 9; Assert.AreEqual(9ul, context.CampaignSeed);
            Action continuation = () => { }; context.BlockingCommandCompleted = continuation;
            Assert.AreSame(continuation, ((ILogicEffectHost)context).BlockingCommandCompleted);
            context.EffectRejected(); var trace = new LogicTraceEntry[1];
            Assert.AreEqual(1, facts.Trace.CopyTo(trace));
            Assert.AreEqual(LogicTraceKind.EffectRejected, trace[0].Kind); Assert.AreEqual(23, trace[0].UniversalTime);
        }
        [TearDown] public void Cleanup() => EventHub.Reset();
    }
}
