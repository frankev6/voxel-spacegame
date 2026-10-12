using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Events;
using UnityEngine.InputSystem;

namespace Game.Input
{
    /// <summary>Owner of the Controls instance and the active input context (architecture §8). Main thread. Implements SPEC-03 §4.3 and §5.1.</summary>
    public sealed class InputService : IDisposable
    {
        const string Scheme = "KeyboardMouse";
        readonly Dictionary<string, string> displayCache = new Dictionary<string, string>(StringComparer.Ordinal);
        float mouseSensitivity = 1f, zoomSensitivity = 1f;
        bool disposed;
        InputActionRebindingExtensions.RebindingOperation rebind;
        InputActionMap rebindingMap;

        /// <summary>Creates Controls, applies the binding overrides, copies the sensitivities and enables Global only (context None).</summary>
        public InputService(Game.Core.Settings.ControlSettingsData controls)
        {
            Controls = new Controls();
            BindingsChanged += displayCache.Clear;
            ApplyBindingOverrides(controls?.BindingOverridesJson);
            if (controls != null)
            {
                MouseSensitivity = controls.MouseSensitivity;
                InvertCameraY = controls.InvertCameraY;
                CameraZoomSensitivity = controls.CameraZoomSensitivity;
            }
            Controls.Global.Enable();
            CurrentContext = InputContext.None;
        }

        public Controls Controls { get; }                                         // generated class; read actions from here
        public InputContext CurrentContext { get; private set; }

        /// <summary>Enables exactly the maps of SPEC-03 decision 2 (Global always) and publishes InputContextChanged once per change.
        /// Sole production caller follows ADR-0044: temporary scene bridge, then UINavigator at UI-004.</summary>
        public void SetContext(InputContext context)
        {
            if (context == CurrentContext) return;
            MapOf(CurrentContext)?.Disable();       // cancels in-progress actions: held buttons read released
            var nextMap = MapOf(context);
            if (nextMap != rebindingMap) nextMap?.Enable();
            var prev = CurrentContext;
            CurrentContext = context;
            EventBus<InputContextChanged>.Publish(new InputContextChanged(prev, context));
        }

        InputActionMap MapOf(InputContext c) => c switch
        {
            InputContext.Flight => Controls.Flight.Get(),
            InputContext.Editor => Controls.Editor.Get(),
            InputContext.Menu => Controls.Menu.Get(),
            InputContext.Dialog => Controls.Dialog.Get(),
            _ => null,                              // None, TextEntry: Global only
        };

        /// <summary>"Flight/Interact" → "F"; composites give the whole composite ("W/S"); unknown → "?" + WarnOnce. Cached until BindingsChanged.</summary>
        public string GetBindingDisplay(string actionPath)
        {
            if (actionPath != null && displayCache.TryGetValue(actionPath, out var cached)) return cached;
            var action = actionPath == null ? null : Controls.asset.FindAction(actionPath, throwIfNotFound: false);
            string result = "?";
            if (action == null) GameLog.WarnOnce("Input.UnknownAction." + actionPath, LogCategories.Input, $"GetBindingDisplay: no action '{actionPath}'");
            else
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var b = bindings[i];
                    if (b.isPartOfComposite) continue;
                    bool inScheme = b.isComposite ? i + 1 < bindings.Count && InScheme(bindings[i + 1]) : InScheme(b);
                    if (!inScheme) continue;
                    result = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                    break;
                }
            }
            if (actionPath != null) displayCache[actionPath] = result;
            return result;
        }

        static bool InScheme(InputBinding b) => b.groups != null && b.groups.Split(InputBinding.Separator).Contains(Scheme);

        public event Action BindingsChanged;                                     // after rebind, reset or ApplyBindingOverrides
        public bool IsRebinding => rebind != null;

        public void StartRebind(string actionPath, int bindingIndex, Action<RebindResult> done)
        {
            CancelRebind();
            var action = disposed || actionPath == null ? null : Controls.asset.FindAction(actionPath, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
            {
                done?.Invoke(new RebindResult(RebindStatus.Failed, actionPath, "?", null));
                return;
            }
            var previous = action.bindings[bindingIndex];
            rebindingMap = action.actionMap;
            rebindingMap.Disable();
            Controls.Global.Disable(); // Escape belongs only to the operation while it is active.
            InputActionRebindingExtensions.RebindingOperation operation = null;
            try
            {
                operation = action.PerformInteractiveRebinding(bindingIndex)
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")
                    .OnMatchWaitForAnother(0.1f);
                rebind = operation;
                operation.OnComplete(op =>
                {
                    string conflict = FindConflict(action, action.bindings[bindingIndex].effectivePath);
                    if (conflict != null) action.ApplyBindingOverride(bindingIndex, previous);
                    FinishRebind(op, actionPath, done, conflict == null ? RebindStatus.Completed : RebindStatus.Conflict, conflict);
                });
                operation.OnCancel(op => FinishRebind(op, actionPath, done, RebindStatus.Cancelled, null));
                operation.Start();
            }
            catch (Exception e)
            {
                // Setup failures must not strand a disabled map or an unmanaged operation.
                if (rebind == operation) rebind = null;
                rebindingMap = null; operation?.Dispose(); RestoreMaps();
                GameLog.Warn(LogCategories.Input, "Could not start rebinding: " + e.Message);
                done?.Invoke(new RebindResult(RebindStatus.Failed, actionPath, GetBindingDisplay(actionPath), null));
            }
        }
        string FindConflict(InputAction action, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var map in Controls.asset.actionMaps)
            {
                if (map != action.actionMap && map != Controls.Global.Get()) continue;
                foreach (var other in map.actions)
                {
                    if (other == action) continue;
                    foreach (var binding in other.bindings)
                        if (!binding.isComposite && string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                            return map.name + "/" + other.name;
                }
            }
            return null;
        }
        void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation, string actionPath, Action<RebindResult> done, RebindStatus status, string conflict)
        {
            if (rebind != operation) return;
            rebind = null; rebindingMap = null;
            operation.Dispose(); RestoreMaps(); displayCache.Clear();
            var result = new RebindResult(status, actionPath, GetBindingDisplay(actionPath), conflict);
            try { if (status == RebindStatus.Completed) BindingsChanged?.Invoke(); }
            finally { done?.Invoke(result); }
        }
        void RestoreMaps()
        {
            Controls.asset.Disable();
            if (disposed) return;
            Controls.Global.Enable(); MapOf(CurrentContext)?.Enable();
        }
        public void CancelRebind() => rebind?.Cancel();

        public string SaveBindingOverridesJson() => Controls.asset.SaveBindingOverridesAsJson();

        /// <summary>"" or null → defaults; invalid JSON → defaults + warning. Raises BindingsChanged (SPEC-03 §5.1).</summary>
        public void ApplyBindingOverrides(string json)
        {
            CancelRebind();
            if (string.IsNullOrEmpty(json)) Controls.asset.RemoveAllBindingOverrides();
            else
            {
                try { Controls.asset.LoadBindingOverridesFromJson(json); }
                catch (Exception e)
                {
                    Controls.asset.RemoveAllBindingOverrides();
                    GameLog.Warn(LogCategories.Input, "Binding overrides are invalid; using the defaults: " + e.Message);
                }
            }
            BindingsChanged?.Invoke();
        }

        public void ResetBinding(string actionPath, int bindingIndex)
        {
            CancelRebind();
            var action = actionPath == null ? null : Controls.asset.FindAction(actionPath, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;
            action.RemoveBindingOverride(bindingIndex); BindingsChanged?.Invoke();
        }
        public void ResetAllBindings() => ApplyBindingOverrides(null);

        public float MouseSensitivity { get => mouseSensitivity; set => mouseSensitivity = Clamp(value); }          // [0.1, 5], from ControlSettingsData
        public bool InvertCameraY { get; set; }
        public float CameraZoomSensitivity { get => zoomSensitivity; set => zoomSensitivity = Clamp(value); }
        static float Clamp(float v) => float.IsNaN(v) ? 1f : Math.Clamp(v, 0.1f, 5f);

        /// <summary>Disables every map and destroys the actions asset (DestroyImmediate outside Play Mode; SPEC-03 open question 4).</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { CancelRebind(); }
            finally
            {
                Controls.asset.Disable();
                if (UnityEngine.Application.isPlaying) Controls.Dispose();
                else UnityEngine.Object.DestroyImmediate(Controls.asset);
            }
        }
    }
}
