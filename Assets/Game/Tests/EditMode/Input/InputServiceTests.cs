using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Input
{
    /// <summary>INP-002: contexts, display strings and settings (SPEC-03 §5.1, decision 2).</summary>
    public sealed class InputServiceTests
    {
        InputService input;
        readonly List<InputContextChanged> changes = new List<InputContextChanged>();
        void OnChanged(InputContextChanged e) => changes.Add(e);

        [SetUp] public void SetUp() { EventHub.Reset(); GameLog.ResetOnceKeys(); changes.Clear(); EventBus<InputContextChanged>.Subscribe(OnChanged); input = new InputService(new ControlSettingsData()); }
        [TearDown] public void TearDown() { input.Dispose(); EventHub.Reset(); }

        string[] EnabledMaps() => input.Controls.asset.actionMaps.Where(m => m.enabled).Select(m => m.name).OrderBy(n => n).ToArray();

        [Test]
        public void Context_EnablesExactMaps()
        {
            CollectionAssert.AreEqual(new[] { "Global" }, EnabledMaps(), "construction: context None");
            var expected = new Dictionary<InputContext, string[]>
            {
                { InputContext.Flight, new[] { "Flight", "Global" } }, { InputContext.Editor, new[] { "Editor", "Global" } },
                { InputContext.Menu, new[] { "Global", "Menu" } }, { InputContext.Dialog, new[] { "Dialog", "Global" } },
                { InputContext.TextEntry, new[] { "Global" } }, { InputContext.None, new[] { "Global" } },
            };
            foreach (var from in expected.Keys)
                foreach (var to in expected.Keys)
                {
                    input.SetContext(from); input.SetContext(to);
                    CollectionAssert.AreEqual(expected[to], EnabledMaps(), $"{from} → {to}");
                    Assert.AreEqual(to, input.CurrentContext);
                }
        }

        [Test]
        public void ContextChanged_PublishedOnce()
        {
            input.SetContext(InputContext.Flight);
            input.SetContext(InputContext.Flight);
            input.SetContext(InputContext.Menu);
            EventHub.DrainAll();
            Assert.AreEqual(2, changes.Count, "no event for a no-op");
            Assert.AreEqual(InputContext.None, changes[0].Previous); Assert.AreEqual(InputContext.Flight, changes[0].Current);
            Assert.AreEqual(InputContext.Flight, changes[1].Previous); Assert.AreEqual(InputContext.Menu, changes[1].Current);
        }

        [Test]
        public void BindingDisplay_Defaults()
        {
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"));
            var thrust = input.GetBindingDisplay("Flight/Thrust");
            StringAssert.Contains("W", thrust); StringAssert.Contains("S", thrust);
            Assert.AreEqual("Escape", input.GetBindingDisplay("Global/Back"));
            Assert.AreSame(input.GetBindingDisplay("Flight/Interact"), input.GetBindingDisplay("Flight/Interact"), "cached");

            var interact = input.Controls.asset.FindAction("Flight/Interact");
            input.ApplyBindingOverrides("{\"bindings\":[{\"action\":\"Flight/Interact\",\"id\":\"" + interact.bindings[0].id + "\",\"path\":\"<Keyboard>/g\",\"interactions\":\"\",\"processors\":\"\"}]}");
            Assert.AreEqual("G", input.GetBindingDisplay("Flight/Interact"), "cache cleared on BindingsChanged");
            LogAssert.Expect(LogType.Warning, new Regex("Binding overrides are invalid"));
            input.ApplyBindingOverrides("{ not json");
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"), "invalid overrides fall back to the defaults");
        }

        [Test]
        public void UnknownPath_Question()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no action 'Flight/Nope'"));
            Assert.AreEqual("?", input.GetBindingDisplay("Flight/Nope"));
            Assert.AreEqual("?", input.GetBindingDisplay("Flight/Nope"), "warned once");
            Assert.AreEqual("?", input.GetBindingDisplay(null));
        }

        [Test]
        public void Sensitivities_CopiedAndClamped()
        {
            using var custom = new InputService(new ControlSettingsData { MouseSensitivity = 2.5f, InvertCameraY = true, CameraZoomSensitivity = 9f });
            Assert.AreEqual(2.5f, custom.MouseSensitivity); Assert.IsTrue(custom.InvertCameraY); Assert.AreEqual(5f, custom.CameraZoomSensitivity);
            custom.MouseSensitivity = 0f; Assert.AreEqual(0.1f, custom.MouseSensitivity);
        }
    }
}
