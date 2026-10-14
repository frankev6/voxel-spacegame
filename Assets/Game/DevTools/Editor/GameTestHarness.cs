// Editor-only CLI harness: drive the running game from a terminal or an AI agent through the Unity CLI
// (`unity command gh_*`, or tools/playtest/g.sh <name>). Generic commands only; game-specific commands are
// added by SPEC-70 tasks in separate files of this assembly. Never ships in builds (Editor-only asmdef).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Globalization;
using Game.App.DevRuntime;
using Unity.Collections;
using Unity.Entities;
using Unity.Pipeline.Commands;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.DevTools.Editor
{
    [InitializeOnLoad]
    public static class GameTestHarness
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                 BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        static GameTestHarness()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += DisposePerf;
            EditorApplication.quitting += DisposePerf;
            if (EditorApplication.isPlaying && SessionState.GetBool(UnfocusedKey, true)) ApplyUnfocusedInput(true);
            if (!EditorApplication.isPlaying) RestoreNativeMice(); // never leave a native mouse disabled after a reload
        }

        static bool RuntimeCommand(string line, out string text)
        {
            text = null;
            if (!EditorApplication.isPlaying || !Game.Core.Services.TryGet<DevRuntimeHost>(out var host) || host == null) return false;
            var result = host.Commands.Execute(line, host.Context);
            text = result.Kind == DevResultKind.Failed ? "failed: " + result.Text : result.Text;
            return true;
        }
        static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------ unfocused input
        // The agent usually drives the Editor while it is NOT the focused app. During play mode only, route all
        // device input to the game regardless of focus; restore the project's settings on exit.

        const string UnfocusedKey = "Game.DevTools.UnfocusedInput";
        static InputSettings.EditorInputBehaviorInPlayMode? savedEditorBehavior;
        static InputSettings.BackgroundBehavior? savedBackground;

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(UnfocusedKey, true)) ApplyUnfocusedInput(true);
            if (s == PlayModeStateChange.ExitingPlayMode) { ApplyUnfocusedInput(false); RemoveAgentMouse(); DisposePerf(); }
        }

        static void ApplyUnfocusedInput(bool on)
        {
            var st = InputSystem.settings;
            if (st == null) return;
            if (on)
            {
                savedEditorBehavior ??= st.editorInputBehaviorInPlayMode;
                savedBackground ??= st.backgroundBehavior;
                st.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                st.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            }
            else
            {
                if (savedEditorBehavior.HasValue) st.editorInputBehaviorInPlayMode = savedEditorBehavior.Value;
                if (savedBackground.HasValue) st.backgroundBehavior = savedBackground.Value;
                savedEditorBehavior = null; savedBackground = null;
            }
        }

        [CliCommand("gh_unfocused_input", "Route keyboard/mouse to the game while the Editor is unfocused (play mode only; restored on exit). On by default.")]
        public static string UnfocusedInput([CliArg("enable", "true/false")] bool enable = true)
        {
            SessionState.SetBool(UnfocusedKey, enable);
            if (EditorApplication.isPlaying) ApplyUnfocusedInput(enable);
            var st = InputSystem.settings;
            return $"unfocused input {(enable ? "ON" : "OFF")}: editorBehavior={st.editorInputBehaviorInPlayMode} background={st.backgroundBehavior}";
        }

        // ------------------------------------------------------------ keyboard (Input System state events)
        // The official simulate_key does not reach Keyboard.current; queue full keyboard states instead.

        static readonly HashSet<Key> HeldKeys = new HashSet<Key>();
        static readonly List<(double until, Key key)> PendingReleases = new List<(double, Key)>();
        static bool pumpHooked;

        static void QueueKeyboardState()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            InputSystem.QueueStateEvent(kb, new KeyboardState(HeldKeys.ToArray()));
        }

        static void Pump()
        {
            if (PendingReleases.Count == 0) return;
            var now = EditorApplication.timeSinceStartup;
            bool changed = false;
            for (int i = PendingReleases.Count - 1; i >= 0; i--)
            {
                if (PendingReleases[i].until > now) continue;
                HeldKeys.Remove(PendingReleases[i].key);
                PendingReleases.RemoveAt(i);
                changed = true;
            }
            if (changed) QueueKeyboardState();
        }

        [CliCommand("gh_key", "Press keys via Input System state events. keys: comma list of Key names (W,Digit1,Space,Escape,Tab...). hold: seconds before release (0 = keep held until --release).")]
        public static string KeyCmd([CliArg("keys", "comma separated Input System Key names")] string keys,
                                    [CliArg("hold", "seconds to hold (default 0.12)")] float hold = 0.12f,
                                    [CliArg("release", "release these keys now")] bool release = false)
        {
            if (RuntimeCommand("key keys=" + DevJsonWriter.Quote(keys) + " hold=" + Number(hold) + " release=" + release, out var result)) return result;
            if (!pumpHooked) { EditorApplication.update += Pump; pumpHooked = true; }
            var parsed = new List<Key>();
            foreach (var k in keys.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                if (!Enum.TryParse<Key>(k, true, out var key)) return "unknown key " + k;
                parsed.Add(key);
            }
            if (release) foreach (var k in parsed) HeldKeys.Remove(k);
            else
            {
                foreach (var k in parsed)
                {
                    HeldKeys.Add(k);
                    PendingReleases.RemoveAll(p => p.key == k);
                    if (hold > 0) PendingReleases.Add((EditorApplication.timeSinceStartup + hold, k));
                }
            }
            QueueKeyboardState();
            return $"{(release ? "released" : "pressed")} {string.Join(",", parsed)}; held now: {string.Join(",", HeldKeys)}";
        }

        // ------------------------------------------------------------ mouse (virtual device)
        // The native mouse keeps pushing its own state (position 0,0 when unfocused). In play mode add a virtual
        // "AgentMouse", disable native mice and make the virtual one current; undone on exit.

        static readonly List<int> DisabledNativeMice = new List<int>();

        static Mouse AgentMouse()
        {
            var vm = InputSystem.devices.OfType<Mouse>().FirstOrDefault(d => d.name == "AgentMouse")
                     ?? InputSystem.AddDevice<Mouse>("AgentMouse");
            foreach (var d in InputSystem.devices.OfType<Mouse>().ToList())
            {
                if (d == vm || !d.enabled) continue;
                InputSystem.DisableDevice(d);
                DisabledNativeMice.Add(d.deviceId);
            }
            vm.MakeCurrent();
            return vm;
        }

        static void RemoveAgentMouse()
        {
            foreach (var d in InputSystem.devices.ToList())
                if (d.name == "AgentMouse") InputSystem.RemoveDevice(d);
            foreach (var id in DisabledNativeMice)
            {
                var d = InputSystem.GetDeviceById(id);
                if (d != null) InputSystem.EnableDevice(d);
            }
            DisabledNativeMice.Clear();
        }

        static void RestoreNativeMice()
        {
            foreach (var d in InputSystem.devices.ToList())
            {
                if (d.name == "AgentMouse") InputSystem.RemoveDevice(d);
                else if (d is Mouse && !d.enabled) InputSystem.EnableDevice(d);
            }
        }

        [CliCommand("gh_mouse", "Move/click a virtual Input System mouse. x,y in game-view pixels (origin bottom-left). button: none|left|right|middle.")]
        public static string MouseCmd([CliArg("x", "x")] float x = -1, [CliArg("y", "y")] float y = -1,
                                      [CliArg("button", "none|left|right|middle")] string button = "none",
                                      [CliArg("down", "true = press, false = release")] bool down = true,
                                      [CliArg("scroll", "scroll delta y")] float scroll = 0,
                                      [CliArg("dx", "mouse delta x (look)")] float dx = 0,
                                      [CliArg("dy", "mouse delta y (look)")] float dy = 0)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            if (RuntimeCommand("mouse x=" + Number(x) + " y=" + Number(y) + " button=" + DevJsonWriter.Quote(button) + " down=" + down + " scroll=" + Number(scroll) + " dx=" + Number(dx) + " dy=" + Number(dy), out var result)) return result;
            var m = AgentMouse();
            var st = new MouseState();
            var pos = x >= 0 ? new Vector2(x, y) : m.position.ReadValue();
            st.position = pos;
            st.delta = new Vector2(dx, dy);
            st.scroll = new Vector2(0, scroll);
            if (down)
            {
                if (button == "left") st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);
                if (button == "right") st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right);
                if (button == "middle") st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Middle);
            }
            InputSystem.QueueStateEvent(m, st);
            return $"mouse pos={pos} button={button} down={down} scroll={scroll} (screen {Screen.width}x{Screen.height})";
        }

        // ------------------------------------------------------------ logs (deduplicated)

        class LogEntry
        {
            public string Type, Msg, Frame;
            public int Count, CountAtMark, FirstSeq, LastSeq;
        }

        static readonly object Lock = new object();
        static readonly Dictionary<string, LogEntry> Logs = new Dictionary<string, LogEntry>();
        static int seq, markSeq;

        static void OnLog(string condition, string stack, LogType type)
        {
            var first = (condition ?? "").Split('\n')[0].Trim();
            if (first.Length > 300) first = first.Substring(0, 300);
            if (first.StartsWith("Command '")) return; // CLI tool noise
            var frame = "";
            if (!string.IsNullOrEmpty(stack))
            {
                foreach (var line in stack.Split('\n'))
                {
                    if (!line.Contains("Assets/Game")) continue;
                    var i = line.IndexOf("(at ", StringComparison.Ordinal);
                    frame = (i >= 0 ? line.Substring(i + 4) : line).TrimEnd(')', ' ', '\r').Replace("Assets/Game/", "");
                    break;
                }
            }
            var key = type + "|" + first;
            lock (Lock)
            {
                seq++;
                if (!Logs.TryGetValue(key, out var e))
                {
                    e = new LogEntry { Type = type.ToString(), Msg = first, Frame = frame, FirstSeq = seq };
                    Logs[key] = e;
                }
                e.Count++;
                e.LastSeq = seq;
            }
        }

        static string LogReport(string level, bool mark, int max)
        {
            var sb = new StringBuilder();
            lock (Lock)
            {
                var items = Logs.Values.Where(e => e.LastSeq > markSeq)
                    .Where(e => level == "all" ||
                                (level == "warn" && e.Type != "Log") ||
                                (level == "error" && (e.Type == "Error" || e.Type == "Exception" || e.Type == "Assert")))
                    .OrderBy(e => e.Type == "Exception" || e.Type == "Error" ? 0 : e.Type == "Warning" ? 1 : 2)
                    .ThenBy(e => e.FirstSeq).ToList();
                int errs = items.Count(e => e.Type == "Exception" || e.Type == "Error" || e.Type == "Assert");
                int warns = items.Count(e => e.Type == "Warning");
                sb.AppendLine($"-- logs since mark: {items.Count} unique ({errs} error kinds, {warns} warning kinds), {seq - markSeq} total lines");
                foreach (var e in items.Take(max))
                {
                    var tag = e.Type == "Exception" ? "EXC" : e.Type == "Error" ? "ERR" : e.Type == "Warning" ? "WRN" : e.Type == "Assert" ? "AST" : "log";
                    sb.AppendLine($"  {tag} x{e.Count - e.CountAtMark}: {e.Msg}{(e.Frame.Length > 0 ? "  <= " + e.Frame : "")}");
                }
                if (items.Count > max) sb.AppendLine($"  ... {items.Count - max} more");
                if (mark) MarkLocked();
            }
            return sb.ToString();
        }

        static void MarkLocked()
        {
            foreach (var e in Logs.Values) e.CountAtMark = e.Count;
            markSeq = seq;
        }

        [CliCommand("gh_logs", "Deduplicated log summary since the last mark (then re-marks). Per-frame spam collapses to one line with a count.")]
        public static string LogsCmd([CliArg("level", "all | warn | error")] string level = "all",
                                     [CliArg("mark", "advance the mark after reading")] bool mark = true,
                                     [CliArg("max", "max unique entries")] int max = 80)
            => LogReport(level, mark, max);

        [CliCommand("gh_mark", "Mark the log position (subsequent gh_logs only show newer lines).")]
        public static string Mark()
        {
            lock (Lock) MarkLocked();
            return "marked at " + seq;
        }

        // ------------------------------------------------------------ snapshot

        [CliCommand("gh_snapshot", "One-call state: frame/time, scenes, visible UI text, camera, ECS summary, deduplicated logs since mark.")]
        public static string Snapshot([CliArg("logs", "include logs since mark")] bool logs = true,
                                      [CliArg("mark", "advance log mark")] bool mark = true,
                                      [CliArg("ecs", "include ECS summary")] bool ecs = true,
                                      [CliArg("ui", "include visible UI text")] bool ui = true)
        {
            var sb = new StringBuilder();
            Section(sb, "frame", () =>
                $"play={EditorApplication.isPlaying} paused(editor)={EditorApplication.isPaused} t={Time.time:F1} " +
                $"timeScale={Time.timeScale} frame={Time.frameCount} fps~{1f / Mathf.Max(Time.smoothDeltaTime, 1e-5f):F0}");
            Section(sb, "scenes", () =>
            {
                var s = new StringBuilder();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    var roots = sc.isLoaded ? sc.GetRootGameObjects() : Array.Empty<GameObject>();
                    s.Append($"{sc.name}{(sc == SceneManager.GetActiveScene() ? "*" : "")}({roots.Count(r => r.activeSelf)}/{roots.Length} active roots) ");
                }
                return s.ToString();
            });
            if (ui) Section(sb, "ui", () => UiText(12, 90));
            Section(sb, "camera", () =>
            {
                var c = Camera.main;
                var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Length;
                if (!c) return $"Camera.main = null; cameras: {string.Join(", ", Camera.allCameras.Select(x => x.name))}; audioListeners={listeners}";
                return $"{c.name} pos={c.transform.position} fwd={c.transform.forward} fov={c.fieldOfView:F0} far={c.farClipPlane} audioListeners={listeners}";
            });
            if (ecs) Section(sb, "ecs", () => EcsSummary(null, 0));
            Section(sb, "session", DevSnapshotSections.Session);
            Section(sb, "player", DevSnapshotSections.Player);
            if (logs) sb.Append(LogReport("all", mark, 40));
            return sb.ToString();
        }

        static void Section(StringBuilder sb, string name, Func<string> f)
        {
            string v;
            try { v = f(); }
            catch (Exception ex) { v = "!! " + ex.GetType().Name + ": " + ex.Message; }
            if (string.IsNullOrEmpty(v)) return;
            sb.Append('[').Append(name).Append("] ").AppendLine(v.TrimEnd());
        }

        // ------------------------------------------------------------ UI Toolkit

        static IEnumerable<UIDocument> Docs() =>
            UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude)
                .Where(d => d.isActiveAndEnabled && d.rootVisualElement != null);

        static bool Shown(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
            {
                var st = p.resolvedStyle;
                if (st.display == DisplayStyle.None || st.visibility == Visibility.Hidden || st.opacity <= 0.01f) return false;
            }
            var wb = e.worldBound;
            return wb.width > 1 && wb.height > 1 && !float.IsNaN(wb.width);
        }

        static string Clip(string s, int n)
        {
            s = (s ?? "").Replace("\n", " ").Replace("\r", " ").Trim();
            return s.Length > n ? s.Substring(0, n) + "…" : s;
        }

        static string UiText(int maxPerDoc, int maxChars)
        {
            var sb = new StringBuilder();
            foreach (var d in Docs().OrderByDescending(d => d.sortingOrder))
            {
                var items = new List<string>();
                foreach (var te in d.rootVisualElement.Query<TextElement>().ToList())
                {
                    if (string.IsNullOrWhiteSpace(te.text) || !Shown(te)) continue;
                    var kind = te is Button ? "btn" : "txt";
                    var nm = string.IsNullOrEmpty(te.name) ? "" : "#" + te.name;
                    items.Add($"{kind}{nm}:'{Clip(te.text, maxChars)}'");
                }
                foreach (var tf in d.rootVisualElement.Query<TextField>().ToList())
                    if (Shown(tf)) items.Add($"field#{tf.name}='{Clip(tf.value, 40)}'");
                if (items.Count == 0) continue;
                sb.Append("\n   ").Append(d.name).Append(": ").Append(string.Join(" | ", items.Take(maxPerDoc)));
                if (items.Count > maxPerDoc) sb.Append($" | …+{items.Count - maxPerDoc}");
            }
            return sb.Length == 0 ? "(no visible UI text)" : sb.ToString();
        }

        [CliCommand("gh_ui", "Visible UI Toolkit text per UIDocument (buttons, labels, fields) with element names.")]
        public static string Ui([CliArg("max", "max items per document")] int max = 60,
                                [CliArg("chars", "max chars per item")] int chars = 200)
            => RuntimeCommand("ui max=" + max + " chars=" + chars, out var result) ? result : UiText(max, chars);

        [CliCommand("gh_click", "Click a visible UI Toolkit element by name (#name) or by text (case-insensitive contains).")]
        public static string Click([CliArg("target", "element name or visible text")] string target,
                                   [CliArg("index", "which match to click when several match")] int index = 0)
        {
            if (RuntimeCommand("click target=" + DevJsonWriter.Quote(target) + " index=" + index, out var result)) return result;
            var t = target.TrimStart('#');
            var matches = new List<(UIDocument doc, VisualElement el)>();
            foreach (var d in Docs())
            {
                foreach (var el in d.rootVisualElement.Query<VisualElement>().ToList())
                {
                    if (!Shown(el)) continue;
                    bool byName = !string.IsNullOrEmpty(el.name) && string.Equals(el.name, t, StringComparison.OrdinalIgnoreCase);
                    bool byText = el is TextElement te && !string.IsNullOrEmpty(te.text) &&
                                  te.text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                  (el is Button || el.parent is Button || el.ClassListContains("unity-button"));
                    if (byName || byText) matches.Add((d, el));
                }
            }
            if (matches.Count == 0) return $"no visible element matching '{target}'. Visible: {UiText(20, 40)}";
            var (doc, hit) = matches[Mathf.Clamp(index, 0, matches.Count - 1)];
            var clickTarget = hit is Button ? hit : (hit.parent as Button) ?? hit;
            if (clickTarget is Button)
            {
                using var ev = NavigationSubmitEvent.GetPooled();
                ev.target = clickTarget;
                clickTarget.SendEvent(ev);
            }
            else
            {
                using (var ev = ClickEvent.GetPooled()) { ev.target = clickTarget; clickTarget.SendEvent(ev); }
                using (var ev2 = PointerDownEvent.GetPooled()) { ev2.target = clickTarget; clickTarget.SendEvent(ev2); }
            }
            return $"clicked {clickTarget.GetType().Name} '{clickTarget.name}' ({Clip((clickTarget as TextElement)?.text, 40)}) in {doc.name}; {matches.Count} match(es)";
        }

        // ------------------------------------------------------------ ECS inspection

        static string EcsSummary(string filter, int maxTypes)
        {
            var sb = new StringBuilder();
            foreach (var w in World.All)
            {
                if (!w.IsCreated) continue;
                var em = w.EntityManager;
                var counts = new Dictionary<string, int>();
                int total = 0;
                using (var chunks = em.GetAllChunks(Allocator.Temp))
                {
                    foreach (var c in chunks)
                    {
                        total += c.Count;
                        using var types = c.Archetype.GetComponentTypes(Allocator.Temp);
                        foreach (var ct in types)
                        {
                            var n = ct.GetManagedType()?.Name ?? ct.ToString();
                            counts.TryGetValue(n, out var v);
                            counts[n] = v + c.Count;
                        }
                    }
                }
                if (total == 0 && filter == null) continue;
                sb.Append($"\n   World '{w.Name}': {total} entities");
                if (filter != null || maxTypes > 0)
                {
                    foreach (var kv in counts.Where(k => filter == null || k.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                                 .OrderByDescending(k => k.Value).Take(maxTypes > 0 ? maxTypes : 400))
                        sb.Append($"\n      {kv.Value,8}  {kv.Key}");
                    continue;
                }
                string C(string n) => counts.TryGetValue(n, out var v) ? v.ToString() : "0";
                sb.Append($" | VoxelGrid={C("VoxelGrid")} Brick={C("Brick")} PhysicsCollider={C("PhysicsCollider")} " +
                          $"PhysicsVelocity={C("PhysicsVelocity")} MaterialMeshInfo={C("MaterialMeshInfo")} Prefab={C("Prefab")} Disabled={C("Disabled")}");
            }
            return sb.Length == 0 ? "(no ECS worlds with entities)" : sb.ToString();
        }

        [CliCommand("gh_ecs", "ECS component counts per world, optionally filtered by component-name substring.")]
        public static string Ecs([CliArg("filter", "component name substring (empty = top types)")] string filter = "",
                                 [CliArg("max", "max types")] int max = 60)
            => EcsSummary(string.IsNullOrEmpty(filter) ? null : filter, max);

        [CliCommand("gh_entities", "List entities having a component (type name): position and the component's field values.")]
        public static string Entities([CliArg("component", "component type name")] string component,
                                      [CliArg("max", "max rows")] int max = 10)
        {
            var w = World.DefaultGameObjectInjectionWorld;
            if (w == null || !w.IsCreated) return "no default world";
            var em = w.EntityManager;
            var t = FindType(component);
            if (t == null) return "type not found: " + component;
            var q = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly(t) }, Options = EntityQueryOptions.IncludeDisabledEntities });
            using var ents = q.ToEntityArray(Allocator.Temp);
            MethodInfo getData = null;
            if (typeof(IComponentData).IsAssignableFrom(t) && t.IsValueType && System.Runtime.InteropServices.Marshal.SizeOf(t) > 0)
                getData = typeof(EntityManager).GetMethods().First(m => m.Name == "GetComponentData" && m.IsGenericMethod && m.GetParameters().Length == 1).MakeGenericMethod(t);
            var sb = new StringBuilder($"{ents.Length} x {component}");
            foreach (var e in ents.Take(max))
            {
                var p = em.HasComponent<LocalToWorld>(e) ? em.GetComponentData<LocalToWorld>(e).Position.ToString() : "-";
                string vals = "";
                try { if (getData != null) vals = Describe(getData.Invoke(em, new object[] { e }), 1, 12); }
                catch (Exception ex) { vals = "!" + ex.GetType().Name; }
                var name = em.GetName(e);
                sb.Append($"\n  {e} {(string.IsNullOrEmpty(name) ? "" : "'" + name + "' ")}pos={p} {Clip(vals, 300)}");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ performance counters

        static Game.App.DevRuntime.Perf.DevPerfCounters editorPerf;
        static void DisposePerf() { editorPerf?.Dispose(); editorPerf = null; }

        [CliCommand("gh_perf", "Profiler counters and optional voxel, combat and last-frame markers; JSON available in and out of play mode.")]
        public static string Perf([CliArg("voxel", "Voxel grid/brick/chunk counters")] bool voxel = false,
            [CliArg("combat", "Projectile, beam, FX and audio counters")] bool combat = false,
            [CliArg("markers", "15 slowest game markers in the last frame")] bool markers = false,
            [CliArg("json", "Emit one JSON object")] bool json = false)
        {
            string command = "perf voxel=" + (voxel ? "true" : "false") + " combat=" + (combat ? "true" : "false") +
                " markers=" + (markers ? "true" : "false") + " json=" + (json ? "true" : "false");
            if (RuntimeCommand(command, out string result)) return result;
            editorPerf ??= new Game.App.DevRuntime.Perf.DevPerfCounters();
            using var context = new DevContext(null);
            return editorPerf.Format(context, voxel, combat, markers, json);
        }

        // ------------------------------------------------------------ reflection helpers

        static Type FindType(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(name, false);
                if (t != null) return t;
            }
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] ts;
                try { ts = a.GetTypes(); } catch { continue; }
                var t = ts.FirstOrDefault(x => x.Name == name);
                if (t != null) return t;
            }
            return null;
        }

        static object Instance(Type t)
        {
            foreach (var n in new[] { "Instance", "instance", "Current" })
            {
                var p = t.GetProperty(n, All);
                if (p != null && p.GetIndexParameters().Length == 0 && p.GetGetMethod(true)?.IsStatic == true)
                {
                    var v = p.GetValue(null);
                    if (v != null) return v;
                }
                var f = t.GetField(n, All);
                if (f != null && f.IsStatic && f.GetValue(null) != null) return f.GetValue(null);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                return UnityEngine.Object.FindAnyObjectByType(t, FindObjectsInactive.Include);
            return null;
        }

        static object Member(object cur, Type type, string part)
        {
            var t = cur?.GetType() ?? type;
            for (var tt = t; tt != null; tt = tt.BaseType)
            {
                var f = tt.GetField(part, All);
                if (f != null) return f.GetValue(f.IsStatic ? null : cur);
                var p = tt.GetProperty(part, All);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(p.GetGetMethod(true).IsStatic ? null : cur);
            }
            throw new MissingMemberException(t?.Name, part);
        }

        static object Resolve(string typeName, string path)
        {
            var t = FindType(typeName) ?? throw new Exception("type not found: " + typeName);
            object cur = null;
            bool first = true;
            foreach (var part in (path ?? "").Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (first)
                {
                    first = false;
                    try { cur = Member(null, t, part); continue; } catch { }
                    cur = Instance(t) ?? throw new Exception("no instance of " + typeName);
                }
                if (cur == null) return null;
                if (int.TryParse(part, out var idx) && cur is IList list) { cur = list[idx]; continue; }
                cur = Member(cur, null, part);
            }
            if (first) cur = Instance(t);
            return cur;
        }

        public static string Describe(object o, int depth = 1, int max = 25)
        {
            switch (o)
            {
                case null: return "null";
                case string s: return "\"" + s + "\"";
                case UnityEngine.Object uo when !uo: return "null(destroyed)";
                case Component c: return $"{c.GetType().Name}@{c.gameObject.name}[{c.gameObject.scene.name}] pos={c.transform.position}";
                case GameObject g: return $"GO {g.name}[{g.scene.name}] active={g.activeInHierarchy} pos={g.transform.position}";
                case IDictionary dict:
                    return $"{o.GetType().Name}({dict.Count}) {{" + string.Join(", ", dict.Keys.Cast<object>().Take(max).Select(k => k + ": " + (depth > 0 ? Describe(dict[k], depth - 1, 5) : "…"))) + (dict.Count > max ? ", …" : "") + "}";
                case IEnumerable en:
                    var l = en.Cast<object>().ToList();
                    return $"{o.GetType().Name}[{l.Count}] [" + string.Join(", ", l.Take(max).Select(x => depth > 0 ? Describe(x, depth - 1, 5) : "…")) + (l.Count > max ? ", …" : "") + "]";
            }
            var t = o.GetType();
            if (t.IsPrimitive || t.IsEnum || o is decimal || t.Namespace == "UnityEngine" || t.Namespace == "Unity.Mathematics") return o.ToString();
            if (depth <= 0) return t.Name;
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Take(max)
                .Select(f =>
                {
                    string v;
                    try { v = Describe(f.GetValue(o), depth - 1, 5); } catch (Exception e) { v = "!" + e.Message; }
                    return f.Name.Replace("k__BackingField", "").Trim('<', '>') + "=" + Clip(v, 120);
                });
            return t.Name + " { " + string.Join("; ", fields) + " }";
        }

        [CliCommand("gh_get", "Read a member path on a type's static or singleton instance, e.g. --type Services --path ...")]
        public static string Get([CliArg("type", "type name")] string type,
                                 [CliArg("path", "dot path (fields/properties/list indices)")] string path = "",
                                 [CliArg("depth", "describe depth")] int depth = 1)
        {
            try { return Describe(Resolve(type, path), depth, 40); }
            catch (Exception e) { return "!! " + e.Message; }
        }

        [CliCommand("gh_invoke", "Invoke a method on a type's static or singleton instance; args is a comma list of primitives.")]
        public static string Invoke([CliArg("type", "type name")] string type,
                                    [CliArg("method", "method name")] string method,
                                    [CliArg("args", "comma separated primitive args")] string args = "",
                                    [CliArg("path", "optional member path to the target object")] string path = "")
        {
            try
            {
                var t = FindType(type) ?? throw new Exception("type not found");
                object target = string.IsNullOrEmpty(path) ? null : Resolve(type, path);
                var argStrs = string.IsNullOrEmpty(args) ? Array.Empty<string>() : args.Split(',').Select(a => a.Trim()).ToArray();
                var tt = target?.GetType() ?? t;
                var m = tt.GetMethods(All).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == argStrs.Length)
                        ?? throw new Exception($"no {method}/{argStrs.Length} on {tt.Name}");
                if (!m.IsStatic && target == null) target = Instance(t) ?? throw new Exception("no instance");
                var ps = m.GetParameters();
                var conv = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    conv[i] = pt.IsEnum ? Enum.Parse(pt, argStrs[i], true) : Convert.ChangeType(argStrs[i], pt, System.Globalization.CultureInfo.InvariantCulture);
                }
                return "ok -> " + Describe(m.Invoke(target, conv), 1);
            }
            catch (TargetInvocationException e) { return "!! threw " + e.InnerException; }
            catch (Exception e) { return "!! " + e.Message; }
        }

        [CliCommand("gh_find", "List scene objects having a component type (name), with positions.")]
        public static string Find([CliArg("type", "component type name")] string type, [CliArg("max", "max")] int max = 30)
        {
            var t = FindType(type);
            if (t == null) return "type not found";
            var objs = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include);
            return $"{objs.Length} x {type}: " + string.Join("; ", objs.Take(max).Select(o => Describe(o, 0)));
        }
    }
}
