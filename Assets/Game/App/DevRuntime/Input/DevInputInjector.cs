#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Game.App.DevRuntime.Input
{
    /// <summary>Dev-only state-event injection and owned-device lifetime (SPEC-70 DEV-004).</summary>
    public sealed class DevInputInjector : IDisposable
    {
        readonly HashSet<Key> held = new HashSet<Key>();
        readonly List<(Key key, double until)> keys = new List<(Key, double)>();
        readonly List<(MouseButton button, double until)> buttons = new List<(MouseButton, double)>();
        readonly List<int> disabledMice = new List<int>();
        Keyboard keyboard;
        bool ownsKeyboard, disposed;
        Mouse mouse;
        MouseState mouseState;
        public Mouse MouseDevice => mouse;
        Keyboard KeyboardDevice()
        {
            if (keyboard != null && keyboard.added) return keyboard;
            keyboard = InputSystem.GetDevice<Keyboard>();
            if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>("AgentKeyboard"); ownsKeyboard = true; }
            return keyboard;
        }
        Mouse AgentMouse()
        {
            if (mouse == null || !mouse.added) mouse = InputSystem.AddDevice<Mouse>("AgentMouse");
            foreach (var device in InputSystem.devices)
            {
                if (!(device is Mouse) || device == mouse || !device.enabled) continue;
                InputSystem.DisableDevice(device); disabledMice.Add(device.deviceId);
            }
            mouse.MakeCurrent(); return mouse;
        }
        public void SetKeys(string names, double hold, bool release)
        {
            CheckAlive(); Duration(hold);
            var parsed = new List<Key>();
            foreach (string name in (names ?? "").Split(','))
            {
                if (!Enum.TryParse(name.Trim(), true, out Key key) || key == Key.None || !Enum.IsDefined(typeof(Key), key))
                    throw new ArgumentException("unknown key '" + name + "'");
                parsed.Add(key);
            }
            KeyboardDevice();
            foreach (var key in parsed)
            {
                for (int i = keys.Count - 1; i >= 0; i--) if (keys[i].key == key) keys.RemoveAt(i);
                if (release) held.Remove(key);
                else { held.Add(key); if (hold > 0) keys.Add((key, Time.unscaledTimeAsDouble + hold)); }
            }
            QueueKeyboard();
        }
        void QueueKeyboard()
        {
            if (keyboard == null || !keyboard.added) return;
            var state = new KeyboardState();
            foreach (var key in held) state.Set(key, true);
            InputSystem.QueueStateEvent(keyboard, state);
        }
        public void SetMouse(float x, float y, string button, bool down, float scroll, float dx, float dy)
        {
            CheckAlive();
            MouseButton? parsed = button switch { "none" => null, "left" => MouseButton.Left, "right" => MouseButton.Right, "middle" => MouseButton.Middle, _ => throw new ArgumentException("unknown mouse button '" + button + "'") };
            AgentMouse();
            if (x >= 0) mouseState.position.x = x;
            if (y >= 0) mouseState.position.y = y;
            mouseState.delta = new Vector2(dx, dy); mouseState.scroll = new Vector2(0, scroll);
            if (parsed.HasValue)
            {
                for (int i = buttons.Count - 1; i >= 0; i--) if (buttons[i].button == parsed.Value) buttons.RemoveAt(i);
                mouseState = mouseState.WithButton(parsed.Value, down);
            }
            QueueMouse();
        }
        void QueueMouse()
        {
            if (mouse != null && mouse.added) InputSystem.QueueStateEvent(mouse, mouseState);
            mouseState.delta = Vector2.zero; mouseState.scroll = Vector2.zero;
        }
        public void PressAction(InputAction action, double seconds)
        {
            CheckAlive(); Duration(seconds);
            if (action == null || action.controls.Count == 0) throw new ArgumentException("action " + action?.name + " has no binding");
            // Controls are resolved in binding order. Gameplay uses the same action and its normal handlers.
            var control = action.controls[0];
            if (control is KeyControl key) { SetKeys(key.keyCode.ToString(), seconds, false); return; }
            if (control.device is Mouse device)
            {
                MouseButton button;
                if (control == device.leftButton) button = MouseButton.Left;
                else if (control == device.rightButton) button = MouseButton.Right;
                else if (control == device.middleButton) button = MouseButton.Middle;
                else throw new ArgumentException("unsupported mouse action control " + control.path);
                SetMouse(-1, -1, button.ToString().ToLowerInvariant(), true, 0, 0, 0);
                if (seconds > 0) buttons.Add((button, Time.unscaledTimeAsDouble + seconds));
                return;
            }
            throw new ArgumentException("unsupported action control " + control.path);
        }
        public void Pump(double now)
        {
            if (disposed) return;
            bool keyChanged = false, mouseChanged = false;
            for (int i = keys.Count - 1; i >= 0; i--)
                if (now >= keys[i].until) { held.Remove(keys[i].key); keys.RemoveAt(i); keyChanged = true; }
            for (int i = buttons.Count - 1; i >= 0; i--)
                if (now >= buttons[i].until) { mouseState = mouseState.WithButton(buttons[i].button, false); buttons.RemoveAt(i); mouseChanged = true; }
            if (keyChanged) QueueKeyboard();
            if (mouseChanged) QueueMouse();
        }
        static void Duration(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentException("hold expects finite non-negative seconds");
        }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(DevInputInjector)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (held.Count > 0) { held.Clear(); QueueKeyboard(); }
            keys.Clear(); buttons.Clear();
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (int id in disabledMice)
            {
                var device = InputSystem.GetDeviceById(id);
                if (device != null && !device.enabled) InputSystem.EnableDevice(device);
            }
            disabledMice.Clear();
            if (ownsKeyboard && keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            mouse = null; keyboard = null;
        }
    }
}
#endif
