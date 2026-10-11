#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace Game.App.DevRuntime.Ui
{
    /// <summary>Visible-text inspection and normal UI Toolkit submission (SPEC-70 DEV-004).</summary>
    public sealed class DevUiQuery
    {
        readonly Func<VisualElement> resolveRoot;
        public DevUiQuery(Func<VisualElement> resolveRoot = null) { this.resolveRoot = resolveRoot ?? ResolveDocument; }
        // UI-003 registers the UIRoot-owned document; no global scene search (SPEC-70 Q14).
        static VisualElement ResolveDocument() => Game.Core.Services.TryGet<UIDocument>(out var document) && document != null ? document.rootVisualElement : null;
        public static bool Shown(VisualElement element)
        {
            if (element.panel == null) return false;
            for (var p = element; p != null; p = p.parent)
            {
                var style = p.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity <= .01f) return false;
            }
            var bounds = element.worldBound;
            return bounds.width > 1 && bounds.height > 1 && !float.IsNaN(bounds.width);
        }
        static string Clip(string text, int max)
        {
            text = (text ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
            return text.Length > max ? text.Substring(0, max) + "…" : text;
        }
        public string Text(int max = 60, int chars = 200)
        {
            if (max < 1 || chars < 1) throw new ArgumentException("max and chars must be positive");
            var root = resolveRoot();
            if (root == null) return "(UI root not available)";
            var text = new StringBuilder(); int count = 0;
            foreach (var element in root.Query<TextElement>().ToList())
            {
                if (string.IsNullOrWhiteSpace(element.text) || !Shown(element)) continue;
                if (count++ >= max) continue;
                if (text.Length > 0) text.Append(" | ");
                text.Append(element is Button ? "btn" : "txt").Append('#').Append(element.name).Append(":'").Append(Clip(element.text, chars)).Append('\'');
            }
            if (count > max) text.Append(" | …+").Append(count - max);
            return text.Length == 0 ? "(no visible UI text)" : text.ToString();
        }
        public DevCommandResult Click(string target, int index = 0)
        {
            if (string.IsNullOrWhiteSpace(target)) return DevCommandResult.Fail("target is required");
            var root = resolveRoot();
            if (root == null) return DevCommandResult.Fail("UI root not available");
            string name = target.TrimStart('#');
            var matches = new List<VisualElement>();
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                if (!Shown(element) || !element.enabledInHierarchy) continue;
                bool byName = !string.IsNullOrEmpty(element.name) && string.Equals(element.name, name, StringComparison.OrdinalIgnoreCase);
                bool byText = !target.StartsWith("#", StringComparison.Ordinal) && element is TextElement text &&
                    !string.IsNullOrEmpty(text.text) && text.text.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!byName && !byText) continue;
                var hit = element is Button ? element : element.GetFirstAncestorOfType<Button>() ?? element;
                if (!matches.Contains(hit)) matches.Add(hit);
            }
            if (index < 0 || index >= matches.Count) return DevCommandResult.Fail("no visible element matching '" + target + "' at index " + index + "; " + Text(20, 40));
            var clicked = matches[index];
            if (clicked is Button) { using var ev = NavigationSubmitEvent.GetPooled(); ev.target = clicked; clicked.SendEvent(ev); }
            else { using var ev = ClickEvent.GetPooled(); ev.target = clicked; clicked.SendEvent(ev); }
            return DevCommandResult.Ok("clicked " + clicked.GetType().Name + " '" + clicked.name + "'");
        }
    }
}
#endif
