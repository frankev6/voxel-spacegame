using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Input
{
    /// <summary>SPEC-03 INP-003: real interactive operations with an isolated keyboard and clock.</summary>
    public sealed class RebindTests : InputTestFixture
    {
        InputService input;
        Keyboard keyboard;
        [SetUp] public override void Setup()
        {
            base.Setup(); EventHub.Reset();
            keyboard = InputSystem.AddDevice<Keyboard>();
            input = new InputService(new ControlSettingsData()); input.SetContext(InputContext.Flight);
        }
        [TearDown] public override void TearDown()
        {
            try { input?.Dispose(); EventHub.Reset(); }
            finally { base.TearDown(); }
        }
        void Match(ButtonControl key)
        {
            Press(key); currentTime += .2; InputSystem.Update(); Release(key);
        }
        [Test] public void Rebind_PersistsThroughJson()
        {
            var results = new List<RebindResult>(); int changes = 0; input.BindingsChanged += () => changes++;
            input.StartRebind("Flight/Interact", 0, results.Add); Assert.IsTrue(input.IsRebinding);
            Match(keyboard.gKey);
            Assert.IsFalse(input.IsRebinding); Assert.AreEqual(1, results.Count); Assert.AreEqual(RebindStatus.Completed, results[0].Status);
            Assert.AreEqual("G", results[0].NewDisplay); Assert.AreEqual("G", input.GetBindingDisplay("Flight/Interact")); Assert.AreEqual(1, changes);
            string json = input.SaveBindingOverridesJson();
            using var restored = new InputService(new ControlSettingsData { BindingOverridesJson = json });
            Assert.AreEqual("G", restored.GetBindingDisplay("Flight/Interact"));
            restored.ResetBinding("Flight/Interact", 0); Assert.AreEqual("F", restored.GetBindingDisplay("Flight/Interact"));
            restored.ApplyBindingOverrides(json); restored.ResetAllBindings(); Assert.AreEqual("F", restored.GetBindingDisplay("Flight/Interact"));
        }
        [Test] public void Conflict_Rejected()
        {
            var results = new List<RebindResult>(); int changes = 0; input.BindingsChanged += () => changes++;
            input.StartRebind("Flight/Interact", 0, results.Add); Match(keyboard.wKey);
            Assert.AreEqual(RebindStatus.Conflict, results[0].Status); Assert.AreEqual("Flight/Thrust", results[0].ConflictActionPath);
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact")); Assert.AreEqual(0, changes);
            // A later rejected attempt also keeps an already customised binding (SPEC-03 Q6).
            input.StartRebind("Flight/Interact", 0, results.Add); Match(keyboard.gKey);
            input.StartRebind("Flight/Interact", 0, results.Add); Match(keyboard.wKey);
            Assert.AreEqual(RebindStatus.Conflict, results[2].Status); Assert.AreEqual("G", input.GetBindingDisplay("Flight/Interact")); Assert.AreEqual(1, changes);
            input.StartRebind("Flight/Interact", 0, results.Add); Match(keyboard.f5Key);
            Assert.AreEqual("Global/QuickSave", results[3].ConflictActionPath); Assert.AreEqual("G", input.GetBindingDisplay("Flight/Interact"));
        }
        [Test] public void Escape_Cancels()
        {
            int back = 0; input.Controls.Global.Back.performed += _ => back++;
            var results = new List<RebindResult>();
            input.StartRebind("Flight/Interact", 0, results.Add);
            input.StartRebind("Flight/Interact", 0, results.Add);
            Assert.AreEqual(1, results.Count); Assert.AreEqual(RebindStatus.Cancelled, results[0].Status);
            Press(keyboard.escapeKey); InputSystem.Update(); Release(keyboard.escapeKey);
            Assert.AreEqual(2, results.Count); Assert.AreEqual(RebindStatus.Cancelled, results[1].Status); Assert.AreEqual(0, back);
            Assert.IsFalse(input.IsRebinding); Assert.IsTrue(input.Controls.Global.enabled); Assert.IsTrue(input.Controls.Flight.enabled);
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"));
            PressAndRelease(keyboard.escapeKey); Assert.AreEqual(1, back, "Back works again after cancellation");
        }
        [Test] public void InvalidJson_Defaults()
        {
            input.StartRebind("Flight/Interact", 0, _ => { }); Match(keyboard.gKey);
            LogAssert.Expect(LogType.Warning, new Regex("Binding overrides are invalid"));
            input.ApplyBindingOverrides("{ invalid json");
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"));
        }
    }
}
