#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Input;
using Game.UI.Services;
using UnityEngine.InputSystem;

namespace Game.App.DevRuntime.Console
{
    /// <summary>Development console presenter over the same registry and operation table used by the CLI.</summary>
    internal sealed class DevConsole : IDisposable
    {
        readonly DevRuntimeHost host;
        readonly DevCommandRegistry registry;
        readonly UINavigator navigator;
        readonly InputAction toggle;
        readonly List<string> lines = new List<string>(200);
        readonly List<string> history = new List<string>(50);
        readonly List<int> pending = new List<int>();
        int historyIndex;
        string draft = string.Empty;
        bool disposed;
        internal DevConsoleScreen Screen { get; }
        internal List<string> Lines => lines;
        internal DevConsole(DevRuntimeHost host, InputService input, UIRoot ui, DevCommandRegistry registry = null)
        {
            this.host = host; this.registry = registry ?? host.Commands; navigator = ui.Navigator;
            Screen = new DevConsoleScreen(this); ui.Register(Screen);
            toggle = input.Controls.Global.DevConsole; toggle.performed += OnToggle;
        }
        void OnToggle(InputAction.CallbackContext context)
        {
            if (disposed) return;
            if (Screen.IsOpen) navigator.Pop(Screen); else navigator.Push(Screen);
        }
        internal void Execute(string command)
        {
            if (disposed || string.IsNullOrWhiteSpace(command)) return;
            if (history.Count == 50) history.RemoveAt(0);
            history.Add(command); historyIndex = history.Count; draft = string.Empty;
            Append("> " + command);
            var result = registry.Execute(command, host.Context); Append(result.Text);
            if (result.Kind == DevResultKind.Pending) pending.Add(result.OperationId);
        }
        internal void Update()
        {
            if (disposed) return;
            for (int i = 0; i < pending.Count;)
            {
                int id = pending[i];
                if (!host.TryGetOperation(id, out var operation)) { Append("op " + id + " unavailable"); pending.RemoveAt(i); }
                else if (operation.Status != DevOperationStatus.Running)
                { Append("op " + id + " " + operation.Status + ": " + operation.Message); pending.RemoveAt(i); }
                else i++;
            }
        }
        internal string Recall(int direction, string current)
        {
            if (history.Count == 0) return current;
            if (historyIndex == history.Count && direction < 0) draft = current;
            historyIndex = Math.Clamp(historyIndex + direction, 0, history.Count);
            return historyIndex == history.Count ? draft : history[historyIndex];
        }
        internal string Complete(string current) => Complete(current, registry.Commands);
        internal static string Complete(string current, IReadOnlyList<IDevCommand> commands)
        {
            string prefix = current.TrimStart();
            if (prefix.Length == 0 || prefix.IndexOfAny(new[] { ' ', '\t' }) >= 0) return current;
            string common = null;
            foreach (var command in commands)
            {
                if (!command.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (common == null) common = command.Name;
                else
                {
                    int length = 0;
                    while (length < common.Length && length < command.Name.Length && common[length] == command.Name[length]) length++;
                    common = common.Substring(0, length);
                }
            }
            return common ?? current;
        }
        void Append(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Replace("\r", string.Empty).Split('\n'))
            { if (lines.Count == 200) lines.RemoveAt(0); lines.Add(line); }
            Screen.Refresh();
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            toggle.performed -= OnToggle;
            if (Screen.IsOpen) navigator.Pop(Screen);
            pending.Clear();
        }
    }
}
#endif
