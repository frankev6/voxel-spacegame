using System.Collections;
using Game.App.DevRuntime;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>DEV-018: production lane/loader/player and real gate conditions; no synthetic arrival events.</summary>
    public sealed class JumpCommandTests : SessionCommandFixture
    {
        [UnityTest] public IEnumerator Adjacent_Succeeds()
        {
            yield return RunCommand("newgame seed=7");
            var galaxy = Services.Get<GalaxyService>();
            var travel = Services.Get<TravelService>();
            Assert.IsTrue(galaxy.TryResolveStarSystem(new ContentId("starsys_drift"), out var target));
            Assert.IsTrue(galaxy.Galaxy.TryFindLane(galaxy.State.CurrentStarSystem, target, out _));
            long entered = Host.Events.Count("StarSystemEntered");
            var result = Host.Commands.Execute("jump to=" + target.Value, Host.Context);
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
            Assert.IsTrue(Host.TryGetOperation(result.OperationId, out var operation));
            Assert.AreEqual(DevOperationStatus.Running, operation.Status);
            // Advance only the spool directly. Streaming, origin placement and event delivery
            // continue through their actual production systems on subsequent frames.
            using (var clocks = EM.CreateEntityQuery(typeof(SimClock)))
            {
                var clock = clocks.GetSingleton<SimClock>(); clock.Tick += (uint)travel.Config.SpoolTicks;
                clocks.SetSingleton(clock);
                travel.Tick(new GalaxyFrameContext(EM, clock.UniversalTime, clock.Tick, 0, 0));
            }
            Assert.AreEqual(TravelPhase.Transit, travel.Phase);
            yield return Until(() => operation.Status != DevOperationStatus.Running, "Jump command completes on actual entry", 30);
            Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
            Assert.Greater(Host.Events.Count("StarSystemEntered"), entered);
            Assert.AreEqual(target, galaxy.State.CurrentStarSystem);
            Assert.AreEqual("true", Host.Commands.Execute("until cond=\"star_system == " + target.Value + "\"", Host.Context).Text);
            // Settle the owner's reveal/arrival before fixture quit attempts its autosave.
            yield return Until(() => travel.Phase == TravelPhase.Idle, "Travel finishes arrival", 30);
        }

        [UnityTest] public IEnumerator Gated_FailsWithReason()
        {
            yield return RunCommand("newgame seed=7");
            var galaxy = Services.Get<GalaxyService>();
            var travel = Services.Get<TravelService>();
            var closed = Host.Commands.Execute("facts set=gate_ark_start_active=0", Host.Context);
            Assert.AreEqual(DevResultKind.Ok, closed.Kind, closed.Text);
            Assert.IsTrue(galaxy.TryResolveStarSystem(new ContentId("starsys_whisper"), out var target));
            var refusal = travel.CanJump(target, JumpSource.Dev);
            Assert.IsFalse(refusal.Accepted); Assert.AreEqual(JumpRefusal.LaneLocked, refusal.Refusal);
            Assert.AreEqual("The gate is dormant.", refusal.Reason);
            long entered = Host.Events.Count("StarSystemEntered");
            var from = galaxy.State.CurrentStarSystem;
            foreach (string name in new[] { target.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "starsys_whisper", DevJsonWriter.Quote(galaxy.GetStarSystemName(target)) })
            {
                var result = Host.Commands.Execute("jump to=" + name, Host.Context);
                Assert.AreEqual(DevResultKind.Failed, result.Kind, result.Text);
                Assert.AreEqual(refusal.Reason, result.Text);
                Assert.AreEqual(0, result.OperationId);
            }
            Assert.AreEqual(TravelPhase.Idle, travel.Phase);
            Assert.AreEqual(from, galaxy.State.CurrentStarSystem);
            Assert.AreEqual(entered, Host.Events.Count("StarSystemEntered"));
        }
    }
}
