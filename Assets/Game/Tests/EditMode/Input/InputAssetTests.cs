using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode.Input
{
    /// <summary>INP-001: the actions asset matches SPEC-03 §4.2.</summary>
    public sealed class InputAssetTests
    {
        Controls controls;
        [SetUp] public void SetUp() { controls = new Controls(); }
        // Controls.Dispose calls Object.Destroy, which edit mode refuses; destroy the asset directly.
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(controls.asset); }

        const string K = "<Keyboard>/", M = "<Mouse>/";
        // map/action, action type, expected control type, plain (non-composite) binding paths; composites are listed as "Composite:part,part,..."
        static readonly (string path, InputActionType type, string control, string[] bindings)[] Table = BuildTable();

        static (string, InputActionType, string, string[])[] BuildTable()
        {
            var t = new List<(string, InputActionType, string, string[])>();
            void B(string map, string a, params string[] b) => t.Add(($"{map}/{a}", InputActionType.Button, "Button", b));
            void V(string map, string a, string control, params string[] b) => t.Add(($"{map}/{a}", InputActionType.Value, control, b));
            B("Global", "Back", K + "escape"); B("Global", "QuickSave", K + "f5"); B("Global", "QuickLoad", K + "f9");
            B("Global", "Screenshot", K + "f12"); B("Global", "DevConsole", K + "f1");
            V("Flight", "Thrust", "Axis", "1DAxis:" + K + "s," + K + "w"); V("Flight", "Strafe", "Axis", "1DAxis:" + K + "q," + K + "e");
            V("Flight", "Turn", "Axis", "1DAxis:" + K + "a," + K + "d");
            B("Flight", "StrafeModifier", K + "shift"); B("Flight", "Boost", K + "leftCtrl"); B("Flight", "Brake", K + "x"); B("Flight", "FirePrimary", M + "leftButton", K + "space");
            V("Flight", "AimDirection", "Vector2"); // Gamepad-only; KeyboardMouse retains cursor aim.
            B("Flight", "FireSecondary", M + "rightButton"); B("Flight", "FireTertiary", K + "g"); B("Flight", "Interact", K + "f");
            for (int i = 1; i <= 4; i++) B("Flight", $"Ability{i}", K + i);
            B("Flight", "TargetNext", K + "tab"); B("Flight", "TargetUnderCursor", K + "r"); B("Flight", "TargetClear", K + "t");
            B("Flight", "AutofireToggle", K + "z"); B("Flight", "Cruise", K + "j"); B("Flight", "CameraOrbitHold", M + "middleButton");
            V("Flight", "CameraLook", "Vector2", M + "delta"); V("Flight", "CameraZoom", "Axis", M + "scroll/y"); B("Flight", "CameraRecenter", K + "c");
            foreach (var map in new[] { "Flight", "Menu" })
            { B(map, "OpenInventory", K + "i"); B(map, "OpenShipLog", K + "l"); B(map, "OpenGalaxyMap", K + "m"); B(map, "OpenSystemMap", K + "n"); B(map, "OpenFleet", K + "k"); }
            for (int i = 1; i <= 9; i++) B("Dialog", $"Choice{i}", K + i);
            B("Dialog", "Continue", K + "space", K + "enter", K + "numpadEnter");
            B("Editor", "Primary", M + "leftButton"); B("Editor", "Secondary", M + "rightButton"); V("Editor", "Pointer", "Vector2", M + "position");
            V("Editor", "ToolWheel", "Axis", M + "scroll/y");
            B("Editor", "Undo", "OneModifier:" + K + "ctrl," + K + "z", "OneModifier:" + K + "leftMeta," + K + "z");
            B("Editor", "Redo", "TwoModifiers:" + K + "ctrl," + K + "shift," + K + "z", "TwoModifiers:" + K + "leftMeta," + K + "shift," + K + "z", "OneModifier:" + K + "ctrl," + K + "y");
            foreach (var (a, k) in new[] { ("Copy", "c"), ("Cut", "x"), ("Paste", "v"), ("SelectAll", "a") })
                B("Editor", a, "OneModifier:" + K + "ctrl," + K + k, "OneModifier:" + K + "leftMeta," + K + k);
            B("Editor", "Delete", K + "delete", K + "backspace");
            B("Editor", "RotateYaw", K + "r"); B("Editor", "RotatePitch", K + "t"); B("Editor", "RotateRoll", K + "g");
            for (int i = 1; i <= 9; i++) B("Editor", $"Tool{i}", K + i);
            B("Editor", "MirrorX", K + "x"); B("Editor", "MirrorY", K + "v"); B("Editor", "MirrorZ", K + "z");
            B("Editor", "SliceToggle", K + "l"); B("Editor", "SliceUp", K + "rightBracket"); B("Editor", "SliceDown", K + "leftBracket");
            B("Editor", "XRay", K + "h"); B("Editor", "BoxModeCycle", K + "b");
            B("Editor", "MoveLeft", K + "leftArrow"); B("Editor", "MoveRight", K + "rightArrow"); B("Editor", "MoveForward", K + "upArrow");
            B("Editor", "MoveBack", K + "downArrow"); B("Editor", "MoveUp", K + "pageUp"); B("Editor", "MoveDown", K + "pageDown");
            B("Editor", "MirrorSelection", K + "m");
            V("Editor", "Orbit", "Vector2", M + "delta"); B("Editor", "OrbitHold", M + "middleButton", "OneModifier:" + K + "alt," + M + "leftButton");
            V("Editor", "Pan", "Vector2", M + "delta"); B("Editor", "PanHold", "OneModifier:" + K + "shift," + M + "middleButton");
            V("Editor", "Zoom", "Axis", M + "scroll/y"); B("Editor", "Focus", K + "f");
            B("Editor", "ViewIso", K + "numpad5"); B("Editor", "ViewTop", K + "numpad7"); B("Editor", "ViewFront", K + "numpad1"); B("Editor", "ViewSide", K + "numpad3");
            t.Add(("Editor/ModShift", InputActionType.PassThrough, "Button", new[] { K + "shift" }));
            t.Add(("Editor/ModCtrl", InputActionType.PassThrough, "Button", new[] { K + "ctrl", K + "leftMeta" }));
            t.Add(("Editor/ModAlt", InputActionType.PassThrough, "Button", new[] { K + "alt" }));
            return t.ToArray();
        }

        static List<string> Describe(InputAction action, string scheme = null)
        {
            var result = new List<string>();
            var bs = action.bindings;
            for (int i = 0; i < bs.Count; i++)
            {
                if (bs[i].isPartOfComposite) continue;
                string groups = bs[i].isComposite && i + 1 < bs.Count ? bs[i + 1].groups : bs[i].groups;
                if (scheme != null && !(groups ?? "").Split(InputBinding.Separator).Contains(scheme)) continue;
                if (!bs[i].isComposite) { result.Add(bs[i].path); continue; }
                var parts = new List<string>();
                for (int j = i + 1; j < bs.Count && bs[j].isPartOfComposite; j++) parts.Add(bs[j].path);
                result.Add(bs[i].path + ":" + string.Join(",", parts));
            }
            return result;
        }

        [Test]
        public void AllActionsPresent()
        {
            var asset = controls.asset;
            CollectionAssert.AreEquivalent(new[] { "Global", "Flight", "Menu", "Dialog", "Editor" }, asset.actionMaps.Select(m => m.name));
            CollectionAssert.AreEqual(new[] { "KeyboardMouse", "Gamepad" }, asset.controlSchemes.Select(s => s.name));
            foreach (var (path, type, control, bindings) in Table)
            {
                var a = asset.FindAction(path, throwIfNotFound: false);
                Assert.IsNotNull(a, path);
                Assert.AreEqual(type, a.type, path);
                Assert.AreEqual(control, a.expectedControlType, path);
                CollectionAssert.AreEqual(bindings, Describe(a, "KeyboardMouse"), path);
            }
            foreach (var map in asset.actionMaps)
                foreach (var a in map.actions)
                    Assert.IsTrue(Table.Any(r => r.path == $"{map.name}/{a.name}"), $"{map.name}/{a.name} is not in SPEC-03 §4.2");
            foreach (var p in new[] { "Flight/CameraZoom", "Editor/ToolWheel", "Editor/Zoom" })
                StringAssert.AreEqualIgnoringCase("Scale(factor=0.008333)", controls.asset.FindAction(p).bindings[0].processors, p);
        }

        [Test]
        public void Global_HasNoCharacterKeys()
        {
            var allowed = new Regex(@"^<Keyboard>/(escape|f([1-9]|1[0-2]))$");
            foreach (var b in controls.Global.Get().bindings)
                if ((b.groups ?? "").Split(InputBinding.Separator).Contains("KeyboardMouse"))
                    Assert.IsTrue(allowed.IsMatch(b.path), $"Global binding {b.path} is not Esc or a function key (SPEC-03 decision 3)");
        }

        [Test]
        public void NoDuplicateBindingsWithinMap()
        {
            // Shared controls the spec defines on purpose, separated by a hold button or by Ctrl (SPEC-03 §4.2 Editor table).
            var shared = new HashSet<string> { "Editor:<Mouse>/delta", "Editor:<Mouse>/scroll/y", "Flight:<Gamepad>/rightStick" }; // AimDirection and hold-gated CameraLook (Q11).
            foreach (var map in controls.asset.actionMaps)
            {
                var seen = new Dictionary<string, string>();
                foreach (var a in map.actions)
                    foreach (var d in Describe(a))
                    {
                        string key = map.name + ":" + d;
                        if (shared.Contains(key)) continue;
                        Assert.IsFalse(seen.TryGetValue(key, out var other), $"{d} is bound to both {other} and {a.name} in {map.name}");
                        seen[key] = a.name;
                    }
            }
        }
    }
}
