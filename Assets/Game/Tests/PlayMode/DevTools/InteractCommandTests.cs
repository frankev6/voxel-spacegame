using System;
using System.Collections;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Overlays;
using Game.App.DevRuntime.Scenarios;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Galaxy.Services;
using Game.Input;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.DevTools
{
    /// <summary>DEV-014: real station geometry, bound input sampling, surface query, event delivery and docking owner.</summary>
    public sealed class InteractCommandTests : GameFlowFixture
    {
        DevRuntimeHost Host => Service<DevRuntimeHost>();
        DevCommandResult Execute(string line) => Host.Commands.Execute(line, Host.Context);
        ScenarioValue Read(string name)
        { Assert.IsTrue(Host.Probes.TryGet(name, out var probe), name); return probe.Read(Host.Context, ReadOnlySpan<ScenarioValue>.Empty); }
        [UnityTest] public IEnumerator Station_OpensInteraction()
        {
            yield return NewGame(42);
            var keyboard = InputSystem.AddDevice<Keyboard>("InteractCommandKeyboard");
            var galaxy = Service<GalaxyService>(); var docking = Service<DockingService>();
            int delivered = 0; BodyInteracted last = default;
            Action<BodyInteracted> handler = value => { delivered++; last = value; };
            EventBus<BodyInteracted>.Subscribe(handler);
            try
            {
                Assert.IsTrue(DevBodyQuery.TryFind(Em, galaxy, "ark_station", out var body, out float radius, out float range));
                var definition = Em.GetComponentData<StarSystemBody>(body);
                string display = galaxy.GetBodyName(definition.Id);
                Assert.IsTrue(DevBodyQuery.TryFind(Em, galaxy, display.ToUpperInvariant(), out var byName, out _, out _)); Assert.AreEqual(body, byName);
                Assert.IsTrue(DevBodyQuery.TryFind(Em, galaxy, galaxy.GetLocationKey(definition.Id), out var byKey, out _, out _)); Assert.AreEqual(body, byKey);
                Assert.IsTrue(DevBodyQuery.TryFind(Em, galaxy, galaxy.GetBodyTemplate(definition.TemplateIndex).StoryTag, out var byTag, out _, out _)); Assert.AreEqual(body, byTag);
                var center = Em.GetComponentData<LocalTransform>(body).Position;
                var moved = Execute("teleport near=ark_station offset=40"); Assert.AreEqual(DevResultKind.Ok, moved.Kind, moved.Text);
                Assert.That(math.distance(center - new float3(0, 0, radius + 40), Em.GetComponentData<LocalTransform>(PlayerRoot).Position), Is.LessThan(.01f));
                foreach (string invalid in new[] { "teleport near=unknown", "teleport near=ark_station offset=-1", "teleport near=ark_station x=1 z=2", "interact body=unknown", "interact typo=true" })
                    Assert.AreEqual(DevResultKind.Failed, Execute(invalid).Kind, invalid);
                var result = Execute("interact body=ark_station"); Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
                var position = Em.GetComponentData<LocalTransform>(PlayerRoot).Position;
                Assert.AreEqual(.5f * range, math.distance(center, position) - radius, .01f, "The teleport offset is measured from the body surface.");
                Assert.AreEqual(0, delivered); Assert.IsFalse(docking.IsDocked, "The command does not call the docking owner directly.");
                Assert.IsTrue(Host.TryGetOperation(result.OperationId, out var operation));
                yield return WaitUntil(() => operation.Status != DevOperationStatus.Running, 12, "Real Interact command completes");
                Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
                StringAssert.Contains("before=", operation.Message); StringAssert.Contains("after=", operation.Message);
                Assert.AreEqual(1, delivered); Assert.AreEqual(definition.Id.System.Value, last.StarSystemValue); Assert.AreEqual(definition.Id.LocalKey, last.BodyKey);
                Assert.AreEqual((byte)InteractionKind.Dock, last.Kind);
                Assert.IsTrue(docking.IsDocked); Assert.AreEqual(definition.Id, docking.DockedBody);
                Assert.IsTrue(Read("interaction.inrange").Bool); Assert.AreEqual(display, Read("interaction.target").String);
                Assert.GreaterOrEqual(Host.Events.Count(nameof(BodyInteracted)), 1);
                var enabled = Execute("overlay name=interaction on=true"); Assert.AreEqual(DevResultKind.Ok, enabled.Kind, enabled.Text);
                Host.Overlays.Enabled = DevOverlayFlags.Interaction;
                Host.Overlays.Gather(Unity.Entities.World.DefaultGameObjectInjectionWorld, Em.GetComponentData<LocalTransform>(PlayerRoot).Position);
                Assert.GreaterOrEqual(Host.Overlays.Lines.Vertices.Length, 66, "At least one 32-segment range circle plus the target line.");
                Assert.AreEqual(2, Host.Overlays.Lines.Vertices.Length % 64);
                Assert.AreEqual(new Color32(0, 255, 0, 255), Host.Overlays.Lines.Vertices[0].Color);
                Execute("overlay name=interaction on=false");
                // Q29: Dock/BodyInteracted is the available owner; this does not claim a dialog route.
                docking.Undock();
            }
            finally
            {
                EventBus<BodyInteracted>.Unsubscribe(handler);
                Host.Overlays.Enabled = DevOverlayFlags.None;
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
            }
        }
        [UnityTest] public IEnumerator UnboundAction_Fails()
        {
            yield return NewGame(42);
            var keyboard = InputSystem.AddDevice<Keyboard>("UnboundInteractKeyboard");
            var input = Service<InputService>(); string overrides = input.SaveBindingOverridesJson();
            var action = input.Controls.Flight.Interact;
            try
            {
                Assert.Greater(action.controls.Count, 0, "The production action initially has a real keyboard binding.");
                for (int i = 0; i < action.bindings.Count; i++) action.ApplyBindingOverride(i, "");
                float3 before = Em.GetComponentData<LocalTransform>(PlayerRoot).Position;
                long events = Host.Events.Count(nameof(BodyInteracted));
                foreach (string line in new[] { "interact body=ark_station", "interact" })
                {
                    var result = Execute(line); Assert.AreEqual(DevResultKind.Failed, result.Kind); Assert.AreEqual(0, result.OperationId);
                    StringAssert.Contains("Flight/Interact has no binding", result.Text);
                }
                Assert.AreEqual(before, Em.GetComponentData<LocalTransform>(PlayerRoot).Position, "Unbound input fails before any teleport.");
                Assert.AreEqual(events, Host.Events.Count(nameof(BodyInteracted))); Assert.IsFalse(Service<DockingService>().IsDocked);
            }
            finally { input.ApplyBindingOverrides(overrides); if (keyboard.added) InputSystem.RemoveDevice(keyboard); }
        }
    }
}
