using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Settings;
using Game.Input;
using Game.Presentation.Presenters;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>HUD-010: five required router cases, actual controls/context owner and bounded device updates.</summary>
    public sealed class ScreenHotkeyRouterTests : InputTestFixture
    {
        sealed class PauseTokens : IPauseService
        {
            readonly HashSet<uint> active = new(); uint next;
            public bool IsPaused => active.Count != 0; public int ActiveTokenCount => active.Count;
            public PauseToken Acquire(string reason) { active.Add(++next); return new PauseToken(this, next); }
            public void Release(PauseToken token) => active.Remove(token.Id);
            public void GetActiveReasons(List<string> result) { result.Clear(); }
        }
        sealed class Clock : IFrameClock { public int FrameCount => 10; public float UnscaledTime => 0; }
        sealed class HudTag { } sealed class InventoryTag { } sealed class MapTag { } sealed class FleetTag { } sealed class ModalTag { }
        sealed class View<T> : UIScreen
        {
            readonly string key;
            internal View(string key) => this.key = key;
            public override string Key => key;
            public override UILayer Layer => typeof(T) == typeof(HudTag) ? UILayer.Hud : typeof(T) == typeof(ModalTag) ? UILayer.Modals : UILayer.Screens;
            public override bool IsRoot => typeof(T) == typeof(HudTag);
            public override InputContext Context => IsRoot ? InputContext.Flight : InputContext.Menu;
            public override bool PausesSimulation => !IsRoot;
            protected override void OnBuild() { }
        }
        sealed class Fixture : IDisposable
        {
            internal readonly InputService Input = new(new ControlSettingsData());
            internal readonly Keyboard Keyboard = InputSystem.AddDevice<Keyboard>();
            internal readonly UINavigator Navigator;
            internal readonly UIScreen Hud = new View<HudTag>("hud"), Inventory = new View<InventoryTag>("inventory"), Map = new View<MapTag>("galaxy-map"), Fleet = new View<FleetTag>("fleet"), Pause = new View<ModalTag>("pause");
            internal readonly ScreenHotkeyRouter Router;
            internal Fixture()
            {
                Navigator = new UINavigator(new PauseTokens(), Input, new Clock(), new VisualElement());
                foreach (var screen in new[] { Hud, Inventory, Map, Fleet, Pause }) Navigator.Register(screen);
                Router = new ScreenHotkeyRouter(Navigator, Input, key => key == "inventory" ? Inventory : key == "galaxy-map" ? Map : key == "fleet" ? Fleet : null);
                Router.Bind(); Navigator.SetRoot(Hud);
            }
            internal void Tap(Key key)
            { InputSystem.QueueStateEvent(Keyboard, new KeyboardState(key)); InputSystem.Update(); InputSystem.QueueStateEvent(Keyboard, new KeyboardState()); InputSystem.Update(); }
            public void Dispose() { Router.Dispose(); Navigator.DisposeOwnedScreens(); Input.Dispose(); }
        }
        [Test] public void FromHud_Opens()
        { using var f = new Fixture(); f.Tap(Key.I); Assert.AreSame(f.Inventory, f.Navigator.Top); Assert.AreEqual(InputContext.Menu, f.Input.CurrentContext); }
        [Test] public void SameKey_Closes()
        { using var f = new Fixture(); f.Tap(Key.I); f.Tap(Key.I); Assert.AreSame(f.Hud, f.Navigator.Top); Assert.AreEqual(InputContext.Flight, f.Input.CurrentContext); }
        [Test] public void OtherKey_Switches()
        { using var f = new Fixture(); f.Tap(Key.I); f.Tap(Key.M); Assert.AreSame(f.Map, f.Navigator.Top); Assert.IsFalse(f.Inventory.IsOpen); Assert.IsTrue(f.Map.IsOpen); }
        [Test] public void ModalOpen_Ignored()
        {
            using var f = new Fixture(); f.Navigator.Push(f.Pause); f.Tap(Key.I); Assert.AreSame(f.Pause, f.Navigator.Top); Assert.IsFalse(f.Inventory.IsOpen);
            f.Navigator.Pop(f.Pause); f.Router.Unbind(); f.Tap(Key.I); Assert.AreSame(f.Hud, f.Navigator.Top);
        }
        static bool HasBinding(InputAction action, string path)
        { foreach (var binding in action.bindings) if (binding.path == path) return true; return false; }
        [Test] public void FleetOnK()
        {
            using var f = new Fixture(); f.Tap(Key.F); Assert.AreSame(f.Hud, f.Navigator.Top);
            f.Tap(Key.K); Assert.AreSame(f.Fleet, f.Navigator.Top); f.Tap(Key.K); Assert.AreSame(f.Hud, f.Navigator.Top);
            // Existing playground interaction/fire controls stay owned by their action maps.
            Assert.IsTrue(HasBinding(f.Input.Controls.asset.FindAction("Flight/Interact"), "<Keyboard>/f"));
        }
    }
}
