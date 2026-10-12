# SPEC-03 — Input and input contexts

| | |
|---|---|
| **Status** | Draft |
| **Area code** | INP (task IDs are `INP-001`, `INP-002`, …) |
| **Module(s)** | `Game.Input` (folder `Assets/Game/Input/`: the actions asset, generated `Controls` class, `InputService`); `Game.Presentation` (folder `Assets/Game/Presentation/Input/`: `PlayerInputSampleSystem`, `InputCameraInput` helpers); `Game.App.EditorHost` (`EditorInputSampler`, ADR-0048) |
| **Depends on specs** | SPEC-02 (`InputContext`, `InputContextChanged`, `IWorldPointerGate`, `ControlSettingsData`, `EventBus<T>`, `GameLog`), SPEC-20 (`PlayerCommand`, `ShipCommandFlags`, `PlayerEdgeCounters` types), SPEC-62 (`MainCameraState` for mouse aim) |
| **Depended on by** | SPEC-01 (constructs `InputService`, binds `PlayerInputSampleSystem`), SPEC-60 (`SetContext`, `Global.Back`, binding display strings), SPEC-61 (rebinding, screen hotkeys), SPEC-62 (camera actions), SPEC-51 (Editor map), SPEC-41 (Dialog map), SPEC-20 (consumes `PlayerCommand`), SPEC-70 (action names for injected input) |
| **Milestone** | M0 (INP-005…INP-007 in M1) |

> Read [01-architecture.md](../01-architecture.md) §5.1 (input latching), [02-conventions.md](../02-conventions.md) and [07-implementation-traps.md](../07-implementation-traps.md) before this spec.

## 1. Purpose

All player input goes through one Input System actions asset and one `InputService`.
- `InputService` turns the active **input context** into exactly one enabled action map plus the always-on `Global` map, so menus, dialog, text fields, the ship editor and flight can never fight over keys.
- Once per frame, `PlayerInputSampleSystem` converts flight input into the `PlayerCommand` singleton.
- Edge-triggered actions are latched as counters, so a press is seen by exactly one fixed tick whatever the frame rate.

This avoids the old prototype's polling of `Keyboard.current` from many scripts, keys that fired in menus, and presses lost or doubled at high and low frame rates (audit 04).

## 2. Scope

**In scope:**
- `Assets/Game/Input/Controls.inputactions`: the maps Global, Flight, Editor, Menu and Dialog, with their actions and default keyboard-and-mouse bindings.
- The generated C# class `Controls`.
- `InputService`:
  - context switching;
  - binding display strings;
  - interactive rebinding;
  - binding overrides persisted through `ControlSettingsData.BindingOverridesJson`;
  - mouse sensitivity and inversion helpers.
- `PlayerInputSampleSystem`:
  - analog flight input;
  - mouse aim onto the flight plane;
  - fire groups;
  - latched edge counters;
  - pointer-over-UI suppression.
- `InputCameraInput : ICameraInput`, the adapter used by SPEC-62.
- `EditorInputSampler`, which builds the `EditorInputFrame` defined by SPEC-51 from the Editor map.

**Out of scope (and who owns it instead):**
- Deciding which context is active: `UINavigator` (SPEC-60), through InputServiceBridge, is the only production caller of `SetContext` (UI-004 / ADR-0047). The former direct scene bridge from ADR-0044 is removed; the temporary App adapter only navigates.
- `PlayerCommand`/`ShipCommand` type definitions and their consumption: SPEC-20.
- Camera behaviour: SPEC-62.
- The settings screen: SPEC-61.
- Settings persistence: SPEC-01.
- Gamepad bindings: M4 (INP-008).
- UI Toolkit's own pointer and keyboard navigation inside panels. UI Toolkit reads the Input System directly; no map is needed for it.

## 3. Decisions (binding)

1. **One actions asset, one generated class, one `InputService` instance.** Nothing else reads `Keyboard.current`, `Mouse.current` or `Input.*`. Exceptions:
   - `PlayerInputSampleSystem` reads `Mouse.current.position` for the aim pointer;
   - the dev harness (SPEC-70) injects input through `InputSystem.QueueStateEvent`.

   Enforced by a `ConventionsTests` rule: `Keyboard.current`, `Mouse.current` and `Gamepad.current` may appear only in `Game.Input`, in `PlayerInputSampleSystem.cs`, in `EditorInputSampler.cs` and in DevTools.
2. **Context → maps** (exact):

   | Context | Enabled maps |
   |---|---|
   | `None` | Global |
   | `Flight` | Global, Flight |
   | `Editor` | Global, Editor |
   | `Menu` | Global, Menu |
   | `Dialog` | Global, Dialog |
   | `TextEntry` | Global |

   Every other map is disabled, so a disabled action reads zero and never fires.
3. **Global contains only Esc and function keys** (SPEC-60 Q3). Character keys typed into a text field can therefore never trigger hotkeys.
4. **Edges are counters, not flags.** For each edge action, `PlayerCommand` holds a `byte` counter that `PlayerInputSampleSystem` increments on each press. The consumer (SPEC-20 `PlayerCommandApplySystem`) compares it with the last value it consumed. Reasons:
   - `PlayerCommand` keeps a single writer (architecture §6.4);
   - a press made during a frame with 0 fixed ticks is not lost;
   - a frame with 6 ticks fires it once.
5. **Mouse aim is what the cursor covers.** A ray from the camera through the cursor is cast against `PhysicsLayers.PickingQuery` (grids and celestial bodies), ignoring the player's own grid; the first hit is the aim point. When nothing is hit, the ray's intersection with the flight plane `Y = 0` is used (SPEC-21 Q6). It uses the previous frame's `MainCameraState`, because SPEC-62 writes it later in the frame. A one-frame camera lag in the aim point is acceptable and deterministic for replay, since replays inject `PlayerCommand` directly (SPEC-71).
6. **Mouse buttons do not fire through UI.** When `IWorldPointerGate.IsWorldPointerAllowed(pointer)` is false, mouse-driven fire groups and `TargetUnderCursor` are suppressed. Keyboard actions are unaffected.
7. **Default bindings are part of this spec (§4.2).** Players change them through rebinding. Overrides are stored as the Input System JSON in `ControlSettingsData.BindingOverridesJson` and applied at boot and on change.
8. **Display strings come from the Input System.** They use `InputBinding.ToDisplayString(DisplayStringOptions.DontIncludeInteractions)` for the first binding of the current control scheme `KeyboardMouse`. UI never hard-codes key names.

## 4. Data model

### 4.1 Asset and generated class (public contract)

- `Assets/Game/Input/Controls.inputactions`, with importer settings "Generate C# Class" on, class `Controls`, namespace `Game.Input`, file `Assets/Game/Input/Controls.cs` (generated; committed).
- One control scheme: `KeyboardMouse` (devices: Keyboard required, Mouse required). `Gamepad` is added in M4.

### 4.2 Maps, actions and default bindings (binding)

Action names are PascalCase and unique within their map. The **action path** used by APIs is `"<Map>/<Action>"`.

**Global**

| Action | Type | Default binding | Used by |
|---|---|---|---|
| `Back` | Button | `<Keyboard>/escape` | SPEC-60 `UINavigator.Back` |
| `QuickSave` | Button | `<Keyboard>/f5` | SPEC-61 |
| `QuickLoad` | Button | `<Keyboard>/f9` | SPEC-61 |
| `Screenshot` | Button | `<Keyboard>/f12` | SPEC-70 |
| `DevConsole` | Button | `<Keyboard>/f1` | SPEC-70 (dev builds only) |

**Flight**

| Action | Type | Default binding | Meaning |
|---|---|---|---|
| `Thrust` | Value / Axis (1D composite) | positive `<Keyboard>/w`, negative `<Keyboard>/s` | forward (+) / reverse (−) |
| `Strafe` | Value / Axis (1D composite) | positive `<Keyboard>/e`, negative `<Keyboard>/q` | strafe right (+) / left (−) |
| `Turn` | Value / Axis (1D composite) | positive `<Keyboard>/d`, negative `<Keyboard>/a` | turn right (+, yaw increases) / left |
| `StrafeModifier` | Button (held) | `<Keyboard>/shift` | Add Turn input to Strafe and set commanded Turn to zero (ADR-0052, FLT-013) |
| `Boost` | Button (held) | `<Keyboard>/leftCtrl` | `ShipCommandFlags.Boost`; suppressed during StrafeModifier |
| `Brake` | Button (held) | `<Keyboard>/x` | `ShipCommandFlags.Brake` |
| `FirePrimary` | Button (held) | `<Mouse>/leftButton`, `<Keyboard>/space` | fire group bit 0 |
| `FireSecondary` | Button (held) | `<Mouse>/rightButton` | fire group bit 1 |
| `FireTertiary` | Button (held) | `<Keyboard>/g` | fire group bit 2 |
| `Interact` | Button (press) | `<Keyboard>/f` | edge `Interact` |
| `Ability1` … `Ability4` | Button (press) | `<Keyboard>/1` … `<Keyboard>/4` | edges `Ability1`…`Ability4` |
| `TargetNext` | Button (press) | `<Keyboard>/tab` | edge `TargetNext` |
| `TargetUnderCursor` | Button (press) | `<Keyboard>/r` | edge `TargetUnderCursor` |
| `TargetClear` | Button (press) | `<Keyboard>/t` | edge `TargetClear` |
| `AutofireToggle` | Button (press) | `<Keyboard>/z` | edge `AutofireToggle` |
| `Cruise` | Button (press) | `<Keyboard>/j` | edge counter `Cruise` (SPEC-32 supercruise, consumed from M4; the action and counter exist from M0) |
| `CameraOrbitHold` | Button (held) | `<Mouse>/middleButton` | SPEC-62 |
| `CameraLook` | Value / Vector2 | `<Mouse>/delta` | SPEC-62 (read only while orbit is held) |
| `CameraZoom` | Value / Axis | `<Mouse>/scroll/y` (processor `scale(factor=0.008333)` so 1 notch = 1) | SPEC-62 |
| `CameraRecenter` | Button | `<Keyboard>/c` | SPEC-62 |
| `OpenInventory` | Button | `<Keyboard>/i` | SPEC-61 hotkeys |
| `OpenShipLog` | Button | `<Keyboard>/l` | SPEC-61 |
| `OpenGalaxyMap` | Button | `<Keyboard>/m` | SPEC-61 |
| `OpenSystemMap` | Button | `<Keyboard>/n` | SPEC-61 |
| `OpenFleet` | Button | `<Keyboard>/k` | SPEC-61 (F is Interact) |

**Menu**: `OpenInventory`, `OpenShipLog`, `OpenGalaxyMap`, `OpenSystemMap` and `OpenFleet`, with the same bindings as in Flight (SPEC-61 Q11), and nothing else.

**Dialog**

| Action | Type | Default binding |
|---|---|---|
| `Choice1` … `Choice9` | Button | `<Keyboard>/1` … `<Keyboard>/9` |
| `Continue` | Button | `<Keyboard>/space`, `<Keyboard>/enter`, `<Keyboard>/numpadEnter` |

**Editor**: SPEC-51 Q8 and SPEC-62 Q1. Modifier combinations use the `OneModifier` composite. The project setting `InputSystem.settings.shortcutKeysConsumeInput = true` (enforced by `ProjectSetup`), so Ctrl+Z does not also fire `Undo`'s plain-Z neighbours.

| Action | Type | Default binding |
|---|---|---|
| `Primary` | Button | `<Mouse>/leftButton` |
| `Secondary` | Button | `<Mouse>/rightButton` |
| `Pointer` | Value / Vector2 | `<Mouse>/position` |
| `ToolWheel` | Value / Axis | `<Mouse>/scroll/y` (scale 0.008333) |
| `Undo` | Button | `OneModifier(<Keyboard>/ctrl, <Keyboard>/z)`, `OneModifier(<Keyboard>/leftMeta, <Keyboard>/z)` |
| `Redo` | Button | `TwoModifiers(ctrl, shift, z)`, `TwoModifiers(leftMeta, shift, z)`, `OneModifier(ctrl, y)` |
| `Copy` / `Cut` / `Paste` | Button | `OneModifier(ctrl, c/x/v)` and the `leftMeta` variants |
| `SelectAll` | Button | `OneModifier(ctrl, a)` and `leftMeta` |
| `Delete` | Button | `<Keyboard>/delete`, `<Keyboard>/backspace` |
| `RotateYaw` / `RotatePitch` / `RotateRoll` | Button | `r` / `t` / `g` (Shift reverses: read `Modifiers.Shift` in the frame) |
| `Tool1` … `Tool9` | Button | `1` … `9` (Place, Line, Box, Fill, Remove, Replace, Paint, Eyedropper, Select) |
| `MirrorX` / `MirrorY` / `MirrorZ` | Button | `x` / `v` / `z` |
| `SliceToggle` / `SliceUp` / `SliceDown` | Button | `l` / `]` / `[` |
| `XRay` | Button | `h` |
| `BoxModeCycle` | Button | `b` |
| `MoveLeft/Right/Forward/Back/Up/Down` | Button | arrows left/right/up/down, `pageUp`, `pageDown` |
| `MirrorSelection` | Button | `m` |
| `Orbit` | Value / Vector2 | `<Mouse>/delta` (read only while `OrbitHold` is pressed) |
| `OrbitHold` | Button | `<Mouse>/middleButton`, `OneModifier(<Keyboard>/alt, <Mouse>/leftButton)` |
| `Pan` | Value / Vector2 | `<Mouse>/delta` (read only while `PanHold` is pressed) |
| `PanHold` | Button | `OneModifier(<Keyboard>/shift, <Mouse>/middleButton)` |
| `Zoom` | Value / Axis | `<Mouse>/scroll/y` (scale 0.008333) — the same physical wheel as `ToolWheel`; SPEC-51 uses `ToolWheel` only while Ctrl is held and `Zoom` otherwise |
| `Focus` | Button | `<Keyboard>/f` |
| `ViewIso` / `ViewTop` / `ViewFront` / `ViewSide` | Button | `numpad5` / `numpad7` / `numpad1` / `numpad3` |
| `Modifiers` | Pass-through Button group: `Shift` (`<Keyboard>/shift`), `Ctrl` (`<Keyboard>/ctrl`, `<Keyboard>/leftMeta`), `Alt` (`<Keyboard>/alt`) | read as three actions `ModShift`, `ModCtrl`, `ModAlt` |

### 4.3 `InputService` (public contract, `Game.Input`, namespace `Game.Input`)

```csharp
/// <summary>Owner of the Controls instance and the active input context (architecture §8). Main thread. Implements SPEC-03 §5.1.</summary>
public sealed class InputService : System.IDisposable
{
    public InputService(Game.Core.Settings.ControlSettingsData controls);   // creates Controls, applies overrides, enables Global
    public Controls Controls { get; }                                        // generated class; read actions from here
    public Game.Core.InputContext CurrentContext { get; }
    public void SetContext(Game.Core.InputContext context);                  // ADR-0044 temporary scene bridge; UI-004 transfers sole ownership to UINavigator
    public string GetBindingDisplay(string actionPath);                      // "Flight/Interact" → "F"; unknown → "?" + WarnOnce
    public event System.Action BindingsChanged;                              // after rebind, reset or ApplyBindingOverrides
    public bool IsRebinding { get; }
    public void StartRebind(string actionPath, int bindingIndex, System.Action<RebindResult> done); // Esc cancels
    public void CancelRebind();
    public string SaveBindingOverridesJson();                                // InputActionAsset.SaveBindingOverridesAsJson()
    public void ApplyBindingOverrides(string json);                          // "" → RemoveAllBindingOverrides; invalid JSON → defaults + Warn
    public void ResetBinding(string actionPath, int bindingIndex);
    public void ResetAllBindings();
    public float MouseSensitivity { get; set; }                              // [0.1, 5], from ControlSettingsData
    public bool InvertCameraY { get; set; }
    public float CameraZoomSensitivity { get; set; }
    public void Dispose();                                                   // disables and disposes Controls
}

public enum RebindStatus : byte { Completed = 0, Cancelled = 1, Conflict = 2, Failed = 3 }
public readonly struct RebindResult
{
    public readonly RebindStatus Status; public readonly string ActionPath; public readonly string NewDisplay; public readonly string ConflictActionPath; // ConflictActionPath set for Conflict
    public RebindResult(RebindStatus s, string path, string display, string conflict) { Status = s; ActionPath = path; NewDisplay = display; ConflictActionPath = conflict; }
}
```

### 4.4 Presentation and App adapters (public contracts, ADR-0048)

```csharp
namespace Game.Presentation.Input
{
    /// <summary>Samples the Flight map into the PlayerCommand singleton once per frame (architecture §5, INP). Managed.</summary>
    [Unity.Entities.UpdateInGroup(typeof(Game.Core.Systems.GameInitGroup))]
    [Unity.Entities.UpdateAfter(typeof(Game.Galaxy.Systems.OriginRebaseSystem))]
    public partial class PlayerInputSampleSystem : Unity.Entities.SystemBase
    {
        public void Bind(Game.Input.InputService input, Game.Core.IWorldPointerGate pointerGate);   // SPEC-01 step 9.1
        public void Unbind();
    }

    /// <summary>ICameraInput (SPEC-62) over InputService actions; mode-aware (Flight vs Editor map).</summary>
    public sealed class InputCameraInput : Game.Presentation.Services.ICameraInput { public InputCameraInput(Game.Input.InputService input); }

    /// <summary>Closest raycast hit that is not on the ignored body (the player's own grid). Implements SPEC-03 §5.2 step 7.5.</summary>
    public struct IgnoreBodyClosestHitCollector : Unity.Physics.ICollector<Unity.Physics.RaycastHit>
    {
        public IgnoreBodyClosestHitCollector(Unity.Entities.Entity ignore, float maxFraction);
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }     // shrinks to the accepted hit's fraction
        public int NumHits { get; private set; }           // 0 or 1
        public Unity.Physics.RaycastHit ClosestHit;
        public bool AddHit(Unity.Physics.RaycastHit hit);  // hit.Entity == ignore → false; else keep it, MaxFraction = hit.Fraction, true
    }

}

namespace Game.App.EditorHost
{
    /// <summary>Builds SPEC-51's EditorInputFrame from the Editor map once per frame.</summary>
    public sealed class EditorInputSampler
    {
        public EditorInputSampler(Game.Input.InputService input, Game.Core.IWorldPointerGate pointerGate);
        public void Sample(ref Game.ShipEditor.Data.EditorInputFrame frame);   // fills every field; actions edge-triggered this frame
    }
}
```

`PlayerCommand` (the singleton) and its edge counters are defined by SPEC-20 (FLT-001). The fields this spec writes:
- `Thrust` (`float2`: x = strafe, y = forward);
- `Turn`;
- `AimWorld` (`float3`, local frame);
- `AimValid` (`byte`);
- `FireGroups` (`uint`);
- `Flags` (`ShipCommandFlags`: `Boost` = 1, `Brake` = 2);
- `Edges` (`PlayerEdgeCounters`, one `byte` per edge: `Interact`, `Ability1`…`Ability4`, `TargetNext`, `TargetUnderCursor`, `TargetClear`, `AutofireToggle`, `Cruise`; 10 bytes);
- `FrameIndex` (`uint`).

SPEC-01 step 11 passes `new InputCameraInput(input)` to `CameraRigService`. SPEC-51's App host creates and receives `Game.App.EditorHost.EditorInputSampler` in EDT-024 (ADR-0048).

## 5. Systems, services and flow

### 5.1 `InputService`

**Constructor:**
1. `Controls = new Controls()`.
2. `ApplyBindingOverrides(controls.BindingOverridesJson)`.
3. Copy the sensitivities.
4. `Controls.Global.Enable()`.
5. Every other map stays disabled; `CurrentContext = None`.

**`SetContext(c)`:**
1. If `c == CurrentContext`, return.
2. Disable the previous context's map, if any.
3. Enable the new one per Decision 2.
4. `prev = CurrentContext; CurrentContext = c`.
5. `EventBus<InputContextChanged>.Publish(new InputContextChanged(prev, c))`.

Disabling a map cancels its in-progress actions, so held buttons read released.

**`GetBindingDisplay(path)`:**
1. Find the action with `asset.FindAction(path, throwIfNotFound: false)`.
2. Take the first binding whose `groups` contains `KeyboardMouse` (composites: return the display of the whole composite, e.g. "W/S").
3. Return `action.GetBindingDisplayString(index, DisplayStringOptions.DontIncludeInteractions)`.

Results are cached in a `Dictionary<string, string>` that is cleared on `BindingsChanged`.

**`StartRebind(path, bindingIndex, done)`:**
1. Disable the action's map.
2. `op = action.PerformInteractiveRebinding(bindingIndex)` with:
   - `.WithCancelingThrough("<Keyboard>/escape")`;
   - `.WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")`;
   - `.OnMatchWaitForAnother(0.1f)`.
3. `OnComplete`: detect a conflict, meaning any other action in the **same map** or in Global whose effective binding path equals the new one.
   - **Conflict:** restore the previous override (or remove it if there was none; Q6) and report `Conflict` with the other action's path. The UI offers to rebind again.
   - **Otherwise:** report `Completed` and raise `BindingsChanged`.
4. `OnCancel` → `Cancelled`.
5. Always dispose `op` and re-enable the map if the current context uses it.

Only one rebind can run at a time: a second call cancels the first.

**`ApplyBindingOverrides(json)`:**
- `"" or null` → `asset.RemoveAllBindingOverrides()`.
- Otherwise `asset.LoadBindingOverridesFromJson(json)` inside try/catch. On exception, call `RemoveAllBindingOverrides()` and `GameLog.Warn`.
- Then raise `BindingsChanged`.

Persisting is the settings presenter's job (SPEC-61): after `Completed`, it writes `SaveBindingOverridesJson()` into an editable settings copy and calls `ISettingsService.Apply(…, Controls)`.

### 5.2 `PlayerInputSampleSystem` (per frame)

Position: `GameInitGroup`, after `OriginRebaseSystem`, before `GridSpawnSystem` (architecture §5), declared here with `[UpdateBefore(typeof(GridSpawnSystem))]` because Voxel cannot reference Presentation (SPEC-10 Q30). Managed `SystemBase`, allowed in Presentation (conventions §2.2). No `RequireForUpdate<GameplayActive>`: it creates the `PlayerCommand` singleton in `OnCreate` and must keep it neutral in menus. Profiler marker `PlayerInputSampleSystem.Sample`.

1. If unbound: write `PlayerCommand` with zero analog, `FireGroups = 0`, `Flags = 0`, `AimValid = 0`, and unchanged counters; return.
2. `cmd = GetSingleton<PlayerCommand>()`. Its counters are kept; everything else is rewritten.
3. `flightOn = input.Controls.Flight.enabled`. If false: zero the analog fields, `FireGroups`, `Flags` and `AimValid`, write the singleton and return. Counters are not touched, so no press is invented or lost.
4. **Analog:** `Thrust = new float2(Strafe.ReadValue<float>(), Thrust.ReadValue<float>())`, each component clamped to [−1, 1]; `Turn = clamp(Turn.ReadValue<float>(), −1, 1)`.
4b. **Strafe modifier (ADR-0052):** when `StrafeModifier.IsPressed()`, set `Thrust.x = clamp(Thrust.x + Turn, −1, 1)` and `Turn = 0`. These are actions, so remapping still works.
5. **Flags:** `Boost` if `Boost.IsPressed()` and the strafe modifier is not held; `Brake` if `Brake.IsPressed()`.
6. **Pointer:**
   - `p = Mouse.current?.position.ReadValue()`; if there is no mouse, `AimValid = 0` and skip to step 8.
   - `allowed = pointerGate.IsWorldPointerAllowed(p)`.
7. **Aim:** if a `MainCameraState` singleton exists and `ScreenSizePx > 0`:
   1. `ndc = (p / ScreenSizePx) * 2 − 1`;
   2. `inv = math.inverse(ViewProjection)`;
   3. `near = inv × (ndc, −1, 1)`, `far = inv × (ndc, 1, 1)`, both divided by w;
   4. `dir = normalize(far − near)`;
   5. **Pick:** if a `PhysicsWorldSingleton` exists: explicitly complete its query dependencies before reading it (singleton access itself does not wait): `ignore` = the first entity of `playerQuery` (`WithAll<PlayerShip>().WithNone<Parent>()`), or `Entity.Null`. Cast `RaycastInput { Start = near, End = near + dir × 5000f, Filter = PhysicsLayers.PickingQuery }` with `new IgnoreBodyClosestHitCollector(ignore, 1f)` (§4.4). On a hit: `AimWorld = hit.Position`, `AimValid = 1`; go to step 8.
   6. **Plane fallback:** `Planar.RayPlaneY(near, dir, 0f, out hit, out _)` → `AimWorld = hit`, `AimValid = 1`; on failure (a ray parallel to the plane or pointing away), keep the previous `AimWorld` and set `AimValid = 0`.

   Without a `MainCameraState`, `AimValid = 0`.
8. **Fire groups:** bit 0 = `FirePrimary.IsPressed() && (allowed || keyboardBindingPressed)` (FND-020, ADR-0053: Space fires even when the pointer overlaps HUD; inactive Flight context still neutralizes it); bit 1 = `FireSecondary.IsPressed() && allowed`; bit 2 = `FireTertiary.IsPressed()`.
9. **Edges:** for each edge action, if `WasPressedThisFrame()` then `counter = (byte)(counter + 1)`. For `TargetUnderCursor`, count only when `allowed`. A frame never counts more than 1 per action.
10. `FrameIndex = (uint)UnityEngine.Time.frameCount`; write the singleton.

The system allocates nothing per frame: action references are cached in `Bind`.

### 5.3 `InputCameraInput.Sample()`

Mode is read from `input.CurrentContext`:
- **Flight:**
  - `OrbitDelta = CameraOrbitHold.IsPressed() ? CameraLook.ReadValue<Vector2>() : 0`;
  - `ZoomSteps = CameraZoom.ReadValue<float>() * CameraZoomSensitivity`;
  - `RecenterPressed = CameraRecenter.WasPressedThisFrame()`;
  - pan and editor view keys read zero.
- Orbit deltas are raw pixels; CameraRigService applies sensitivity/inversion exactly once (CAM-005, Q7).
- **Editor:** the same rules, using the Editor map's `OrbitHold`/`Orbit`, `PanHold`/`Pan`, `Zoom` (only while Ctrl is not held), `Focus` and `View*`.
- **Other contexts:** everything reads zero.

### 5.4 `EditorInputSampler.Sample(ref frame)`

1. `frame.PointerScreen = Pointer.ReadValue<Vector2>()`; `PointerAllowed = gate.IsWorldPointerAllowed(...)`.
2. `PrimaryPressed/Held/Released` come from `Primary`'s `WasPressedThisFrame`/`IsPressed`/`WasReleasedThisFrame`. `SecondaryPressed/Released` come from the corresponding press/release edges (SPEC-51 has no SecondaryHeld field).
3. `Shift/Ctrl/Alt` come from the modifier actions.
4. `ToolWheel = ModCtrl ? ToolWheel.ReadValue<float>() : 0`.
5. `Actions` = OR of the `EditorActions` bits whose action `WasPressedThisFrame()`, using the mapping table of SPEC-51 §4 (one bit per action of §4.2 Editor).

When the Editor map is disabled, set `frame = default`.

## 6. Public API

| API | Preconditions | Postconditions / errors |
|---|---|---|
| `InputService.SetContext` | Main thread; sole caller follows ADR-0044, then `InputServiceBridge` | Exactly the maps of Decision 2 enabled; `InputContextChanged` published once per actual change |
| `GetBindingDisplay(path)` | — | Display string; unknown path → `"?"` and `WarnOnce` |
| `StartRebind` | Not already rebinding (else cancels the previous) | `done` called exactly once |
| `ApplyBindingOverrides(json)` | — | Never throws; invalid JSON → defaults + warning |
| `PlayerInputSampleSystem.Bind` | Called once by SPEC-01 | Sampling starts next frame |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Key held while a menu opens | Map disabled → reads zero; reopening flight does not produce a phantom press (`WasPressedThisFrame` false for a held key at enable) |
| Two presses in one frame | Counter +1 (one frame, one press) |
| Press during a frame with 0 fixed ticks (paused) | Paused means the Flight map is disabled (pause screens set `Menu`), so no press is counted; unpaused low-frame-rate frames still count, and the consumer sees it next tick |
| Frame with 6 fixed ticks | Consumer sees the counter change in the first tick only |
| Counter wraps 255 → 0 | Consumer compares with `!=`, so wrap is harmless |
| Mouse over a HUD button | Primary/secondary fire suppressed; keyboard fire still works |
| No camera yet (first frame) | `AimValid = 0`; weapons with `AimValid == 0` hold their current aim (SPEC-21) |
| Window loses focus | `Application.runInBackground = true` (project setting); the Input System keeps state; harness input still works (SPEC-70) |
| Rebinding to a key already used in the same map | `Conflict` result; old binding kept |
| Rebind cancelled with Esc | `Cancelled`; Esc does not also trigger `Global/Back` (rebind consumes it; `Global` re-enabled after completion) |
| Corrupt `BindingOverridesJson` | Defaults; warning |

## 8. Performance budget

| Gate | Target | Measured by |
|---|---|---|
| `PlayerInputSampleSystem` | ≤ 0.03 ms main thread, 0 B allocated | `PlayerInputSampleSystemTests.NoAlloc`, profiler marker |
| `InputService.SetContext` | ≤ 0.05 ms | EditMode timing test (100 switches ≤ 5 ms) |

**Scale notes:** input cost is O(actions) per frame (fewer than 60), and the system touches no entities other than the one singleton.

## 9. Tasks

### INP-001 — Public contract types and the actions asset
- **Milestone:** M0
- **Tier:** standard — transcribes §4.2 bindings and §4.3/§4.4 signatures
- **Depends on:** `CORE-001`
- **Do:** Create `Controls.inputactions` with every map, action and default binding of §4.2, generate `Controls.cs`, and create `InputService`, `RebindStatus`, `RebindResult` with bodies throwing `NotImplementedException`. The Presentation types (§4.4) are created by INP-005 because they need FLT-001 and CAM-001.
- **Files:** `Assets/Game/Input/{Controls.inputactions,Controls.cs,InputService.cs,RebindStatus.cs,RebindResult.cs}`
- **Acceptance criteria:**
  - [ ] Every action of §4.2 exists with the stated type and default binding.
  - [ ] The Global map contains no character key.
  - [ ] `tools/compile.sh` clean.
- **Tests:** `InputAssetTests.AllActionsPresent` (table of §4.2), `.Global_HasNoCharacterKeys`, `.NoDuplicateBindingsWithinMap`.

### INP-002 — InputService contexts and display strings
- **Milestone:** M0
- **Tier:** standard — Decision 2 table and §5.1 steps
- **Depends on:** `INP-001`
- **Do:** Implement the constructor, `SetContext`, `GetBindingDisplay` with cache, `Dispose`, the sensitivities, and the `ConventionsTests` rule of Decision 1.
- **Files:** `Assets/Game/Input/InputService.cs`; `Assets/Game/Tests/EditMode/Input/InputServiceTests.cs`; `Assets/Game/Tests/EditMode/Project/ConventionsTests.cs`
- **Acceptance criteria:**
  - [ ] For each context, exactly the maps of Decision 2 are enabled.
  - [ ] `InputContextChanged` is published once per change and never for a no-op.
  - [ ] `GetBindingDisplay("Flight/Interact") == "F"` with defaults; `"Flight/Thrust"` returns a composite display containing "W" and "S".
- **Tests:** `InputServiceTests.Context_EnablesExactMaps` (all 6), `.ContextChanged_PublishedOnce`, `.BindingDisplay_Defaults`, `.UnknownPath_Question`.

### INP-003 — Rebinding and overrides
- **Milestone:** M0
- **Tier:** standard — algorithm in §5.1 with fixed results
- **Depends on:** `INP-002`
- **Do:** Implement `StartRebind`, `CancelRebind`, conflict detection, `SaveBindingOverridesJson`, `ApplyBindingOverrides`, `ResetBinding`, `ResetAllBindings`.
- **Files:** `Assets/Game/Input/InputService.cs`; `Assets/Game/Tests/EditMode/Input/RebindTests.cs`
- **Acceptance criteria:**
  - [ ] Rebinding `Flight/Interact` to G (simulated with `InputTestFixture`) → display "G"; `SaveBindingOverridesJson` → `ApplyBindingOverrides` on a new service restores "G".
  - [ ] Rebinding `Flight/Interact` to W reports `Conflict` with `Flight/Thrust`, and `Interact` stays "F".
  - [ ] Esc cancels and does not trigger `Global/Back`.
- **Tests:** `RebindTests.Rebind_PersistsThroughJson`, `.Conflict_Rejected`, `.Escape_Cancels`, `.InvalidJson_Defaults`.

### INP-004 — Context integration with SPEC-60
- **Milestone:** M1
- **Tier:** standard — wiring of existing APIs
- **Depends on:** `INP-002`, `UI-001`
- **Do:** Verify SPEC-60's `InputServiceBridge` against this API, and add the PlayMode test that a held W key reads zero after a pause menu opens and produces no phantom press after it closes.
- **Files:** `Assets/Game/Tests/PlayMode/Input/ContextIntegrationTests.cs`
- **Acceptance criteria:**
  - [ ] With W held, opening pause sets `Thrust` to 0 within the same frame; closing it gives `Thrust == 1`, and no edge counter changed.
- **Tests:** `ContextIntegrationTests.HeldKey_AcrossPause`.

### INP-005 — PlayerInputSampleSystem
- **Milestone:** M1
- **Tier:** frontier — F5/F1: the latching contract every frame rate depends on (architecture §5.1); determinism gate input
- **Depends on:** `INP-002`, `FLT-001`, `CAM-001`, `PHY-001`
- **Do:** Implement `PlayerInputSampleSystem` (§5.2) with `Bind`/`Unbind`, and `IgnoreBodyClosestHitCollector` (§4.4). Add the bind call at SPEC-01 §5.2 step 9.1: `world.GetExistingSystemManaged<PlayerInputSampleSystem>().Bind(input, ui)`.
- **Files:** `Assets/Game/Presentation/Input/{PlayerInputSampleSystem,IgnoreBodyClosestHitCollector}.cs`; `Assets/Game/App/Boot/GameComposition.cs` (one line); `Assets/Game/Tests/EditMode/Input/PlayerInputSampleSystemTests.cs`
- **Acceptance criteria:**
  - [x] With `InputTestFixture`: W → `Thrust.y == 1`; Q → `Thrust.x == −1`; D → `Turn == 1`.
  - [x] Pressing F once over 3 frames increments `Edges.Interact` by exactly 1.
  - [x] Aim with a known `MainCameraState` (top-down orthographic-equivalent matrices) maps the screen centre to the camera's ground point within 1e-3 m.
  - [x] The pointer over a blocking UI rectangle (fake gate) suppresses bits 0 and 1 but not bit 2.
  - [x] Pick: a static box collider (filter of a grid) 100 m in front of the camera under the cursor → `AimWorld` on its near face within 1e-3 m; the same box carrying `PlayerShip` is ignored and the plane point is used; no collider → plane point.
  - [x] Pressing J once increments `Edges.Cruise` by exactly 1.
  - [x] 0 B allocated per update.
- **Tests:** `PlayerInputSampleSystemTests.Aim_PicksCollider`, `.Aim_IgnoresPlayer`, `.Aim_PlaneFallback`, `.Cruise_Counts`, `PlayerInputSampleSystemTests.Analog_Mapping`, `.Edge_CountsOnce`, `.Aim_ScreenCenter`, `.PointerBlocked_SuppressesMouseFire`, `.Unbound_Neutral`, `.NoAlloc`.

### INP-006 — Camera and editor samplers
- **Milestone:** M1
- **Tier:** standard — §5.3 and §5.4 mapping tables
- **Depends on:** `INP-002`, `CAM-001`
- **Do:** Extend CAM-005’s Flight `InputCameraInput` with its Editor branch (§5.3), test the shared adapter, and implement `Game.App.EditorHost.EditorInputSampler` (§5.4). Declare the exact `EditorActions` and `EditorInputFrame` types from SPEC-51 §4.2 now; EDT-001 reuses them (ADR-0048).
- **Files:** `Assets/Game/Presentation/Input/InputCameraInput.cs`; `Assets/Game/App/EditorHost/EditorInputSampler.cs`; `Assets/Game/ShipEditor/Data/{EditorActions,EditorInputFrame}.cs`; `Assets/Game/Tests/EditMode/Input/SamplerTests.cs`
- **Acceptance criteria:**
  - [x] Orbit delta is zero unless the hold is pressed; the adapter/rig path applies Y inversion once.
  - [x] In the Editor, Ctrl+scroll feeds `ToolWheel` and plain scroll feeds `Zoom`.
  - [x] Ctrl+Z sets `Undo` and not `MirrorZ`.
- **Tests:** `SamplerTests.Orbit_OnlyWhileHeld`, `.InvertY`, `.CtrlScroll_ToolWheel`, `.CtrlZ_UndoOnly`.

### INP-007 — Frame-rate independence test
- **Milestone:** M1
- **Tier:** standard — test of specified behaviour
- **Depends on:** `INP-005`, `SPEC-20`
- **Do:** PlayMode test: inject a scripted sequence of presses and holds at simulated 30, 60 and 144 FPS (`Time.captureDeltaTime`). Assert the same number of consumed `Interact` and `Ability1` edges, and the same total thrust-seconds (±1 tick), in the player ship's `ShipCommand` history recorded by a test system.
- **Files:** `Assets/Game/Tests/PlayMode/Input/FrameRateIndependenceTests.cs`
- **Acceptance criteria:**
  - [x] Edge counts are identical at all three rates; held-thrust tick counts differ by at most 1.
- **Tests:** `FrameRateIndependenceTests.Edges_SameAt30_60_144`, `.Thrust_TickCountsMatch`.

### INP-008 — Gamepad scheme
- **Milestone:** M4
- **Tier:** standard — bindings only
- **Depends on:** `INP-003`
- **Do:** Add the `Gamepad` control scheme with bindings for Flight (left stick thrust/strafe, right stick aim direction mapped to `AimWorld` = ship position + 50 m × direction, triggers fire, shoulders boost/brake), Menu and Dialog. Update `GetBindingDisplay` to use the last used device's scheme.
- **Files:** `Assets/Game/Input/Controls.inputactions`; `Assets/Game/Presentation/Input/{PlayerInputSampleSystem,IgnoreBodyClosestHitCollector}.cs`; tests
- **Acceptance criteria:**
  - [ ] Every Flight action has a gamepad binding; the display switches on device change.
- **Tests:** `GamepadTests.AllFlightActionsBound`, `.DisplaySwitchesWithDevice`.

## 10. Open questions

| # | Question | Default if unanswered | Decider | Answer |
|---|---|---|---|---|
| 1 | Flight controls: A/D turn and Q/E strafe, or mouse-facing ship? | A/D turn, Q/E strafe; mouse aims weapons only | Owner |  |
| 2 | Interact on F (and Fleet screen on K) | Yes | Owner |  |
| 3 | Fire groups beyond 3 | 3 groups in v1 (LMB, RMB, Space) | Owner |  |
| 4 | The generated `Controls.Dispose()` calls `Object.Destroy(asset)`, which logs an error in edit mode, so EditMode tests that dispose an `InputService` fail on the unexpected log (found in INP-001) | `InputService.Dispose` disables the maps, then destroys `Controls.asset` with `Object.DestroyImmediate` when `!Application.isPlaying` and calls `Controls.Dispose()` otherwise | Tech lead |  |
| 5 | No task sets `InputSystem.settings.shortcutKeysConsumeInput = true` in `ProjectSetup` (§4.2 Editor) | INP-002 adds it to `ProjectSetup.ApplyAll`/`Check` with a `ProjectSettingsGuardTests` case | Tech lead |  |

| 6 | Removing a conflicting override loses a previously successful custom binding, contrary to §7’s “old binding kept” | **Answer (resolved in INP-003):** capture and restore the prior binding overrides on conflict; defaults remain defaults when no override existed. Disable Global while rebinding so cancellation cannot fire Back. ADR-0021 permits the already-installed InputTestFixture assembly reference required by the task. | Tech lead |  |

| 7 | Camera adapter and rig both apply sensitivity/inversion, and CAM-005/INP-006 duplicate adapter ownership | Raw pixel deltas; rig owns orbit settings; one adapter in Game.Presentation.Input | Tech lead | Resolved CAM-005 / SPEC-62 Q12: CAM-005 implements Flight and app binding; INP-006 adds Editor and the listed adapter tests (InvertY checks the adapter/rig path). Zoom sensitivity is applied once in the adapter. |


| 8 | Player sampler precedes UI composition and origin rebasing; singleton reads do not implicitly wait | Bind with no pointer blocker until UIRoot exists, defer the absent ordering edge, explicitly complete physics readers | Tech lead | Resolved 2026-10-12: null pointer gate permits world pointer input while there is no UI; disabled Flight/unbound service stays neutral. UI-003 rebinds the actual gate; FND-017 owns temporary scene/pause contexts. GAL-009 must run before sampling when introduced. Physics query completion is explicit, preserving the stated read boundary without assuming singleton access waits. |

| 9 | Editor sampler placement violates the Presentation boundary and its frame does not exist before EDT-001 | App host; stage the canonical input types in INP-006 | Tech lead | Resolved by [ADR-0048](../decisions/ADR-0048-editor-input-sampler-boundary.md): App/EditorHost sampler uses Game.ShipEditor.Data frame/actions; no assembly changes or enum copy. EDT-024 composes it later. |

## 11. References

- Architecture §5 (`PlayerInputSampleSystem`), §5.1 (latching), §6.4 (`PlayerCommand`).
- Legacy bindings: `Import/Legacy/input/legacy_InputSystem_Actions.inputactions` (reference only; Fire on Space and Interact on E were replaced).
- Unity Input System: `PerformInteractiveRebinding`, `SaveBindingOverridesAsJson`, `InputTestFixture`, `shortcutKeysConsumeInput`.
