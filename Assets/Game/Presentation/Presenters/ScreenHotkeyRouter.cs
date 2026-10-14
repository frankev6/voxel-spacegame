using System;
using Game.Core;
using Game.Input;
using Game.UI.Services;
using Game.UI.Views;
using UnityEngine.InputSystem;

namespace Game.Presentation.Presenters
{
    /// <summary>SPEC-61 §5.5/HUD-010: existing Flight/Menu screen actions; navigator remains the sole stack/input owner.</summary>
    public sealed class ScreenHotkeyRouter : IDisposable
    {
        readonly UINavigator navigator;
        readonly InputService input;
        readonly Func<string, UIScreen> resolve;
        readonly Action<string> unavailable;
        readonly InputAction[] actions = new InputAction[10];
        readonly Action<InputAction.CallbackContext>[] handlers = new Action<InputAction.CallbackContext>[10];
        bool bound, disposed;
        public ScreenHotkeyRouter(UINavigator navigator, InputService input, Func<string, UIScreen> resolve, Action<string> unavailable = null)
        {
            this.navigator = navigator ?? throw new ArgumentNullException(nameof(navigator)); this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve)); this.unavailable = unavailable;
            string[] names = { "OpenInventory", "OpenShipLog", "OpenGalaxyMap", "OpenSystemMap", "OpenFleet" };
            string[] keys = { "inventory", "ship-log", "galaxy-map", "system-map", "fleet" };
            for (int map = 0; map < 2; map++) for (int i = 0; i < names.Length; i++)
            {
                int index = map * 5 + i; string key = keys[i];
                actions[index] = input.Controls.asset.FindAction((map == 0 ? "Flight/" : "Menu/") + names[i], true);
                handlers[index] = _ => Route(key);
            }
        }
        public void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ScreenHotkeyRouter)); if (bound) return; bound = true;
            for (int i = 0; i < actions.Length; i++) actions[i].performed += handlers[i];
        }
        public void Unbind() { if (!bound) return; bound = false; for (int i = 0; i < actions.Length; i++) actions[i].performed -= handlers[i]; }
        void Route(string key)
        {
            if (!bound || input.IsRebinding || input.CurrentContext == InputContext.TextEntry) return;
            var top = navigator.Top; if (top == null) return;
            // Screen keys are unique, canonical UIScreen identities enforced by UINavigator registration.
            if (top.Key == key) { navigator.Pop(top); return; }
            if (top.Key != "hud" && !IsHotkeyScreen(top.Key)) return;
            var target = resolve(key); if (target == null) { unavailable?.Invoke("This screen is not available yet"); return; }
            if (target.Key != key) throw new InvalidOperationException("HUD screen resolver returned the wrong screen.");
            if (top.Key != "hud") navigator.Pop(top);
            navigator.Push(target);
        }
        static bool IsHotkeyScreen(string key) => key == "inventory" || key == "ship-log" || key == "galaxy-map" || key == "system-map" || key == "fleet";
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; }
    }
}
