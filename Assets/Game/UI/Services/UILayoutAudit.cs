using System;
using System.Collections.Generic;
using Game.UI.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>SPEC-60 §5.12: resolved geometry audit, called after panel layout by tests and the harness.</summary>
    public static class UILayoutAudit
    {
        const float Tolerance = .5f;
        public static void Run(VisualElement root, Rect viewport, List<LayoutIssue> results)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            var panels = new List<VisualElement>();
            var regions = new List<VisualElement>();
            for (var ancestor = root.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor.resolvedStyle.display == DisplayStyle.None) return;
                if (ancestor.ClassListContains(UIStyleClasses.Panel)) panels.Add(ancestor);
            }
            Visit(root, viewport, panels, regions, results);
            for (int i = 0; i < regions.Count; i++)
                for (int j = i + 1; j < regions.Count; j++)
                {
                    Rect a = regions[i].worldBound, b = regions[j].worldBound;
                    float width = Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin));
                    float height = Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
                    if (width * height > 1)
                        Add(results, LayoutIssueKind.RegionOverlap, regions[j], b, a);
                }
        }
        static void Visit(VisualElement element, Rect viewport, List<VisualElement> panels,
            List<VisualElement> regions, List<LayoutIssue> results)
        {
            if (element.resolvedStyle.display == DisplayStyle.None) return;
            // Scroll content is deliberately larger than its viewport. Audit the viewport/chrome,
            // not the content container or its children; their clipping/scrolling has its own tests.
            if (IsScrollContent(element)) return;
            Rect bounds = element.worldBound;
            if (!string.IsNullOrEmpty(element.name))
            {
                if (!Contains(viewport, bounds)) Add(results, LayoutIssueKind.OutOfViewport, element, bounds, viewport);
                foreach (var panel in panels)
                    if (!Contains(panel.worldBound, bounds)) Add(results, LayoutIssueKind.OutsidePanel, element, bounds, panel.worldBound);
                if ((element is Label || element is Button) && element is TextElement text && !string.IsNullOrEmpty(text.text))
                    AuditText(text, results);
            }
            if (element.ClassListContains(UIStyleClasses.HudRegion)) regions.Add(element);
            bool isPanel = element.ClassListContains(UIStyleClasses.Panel);
            if (isPanel) panels.Add(element);
            for (int i = 0; i < element.hierarchy.childCount; i++) Visit(element.hierarchy[i], viewport, panels, regions, results);
            if (isPanel) panels.RemoveAt(panels.Count - 1);
        }
        static bool IsScrollContent(VisualElement element)
        {
            for (var ancestor = element.parent; ancestor != null; ancestor = ancestor.parent)
                if (ancestor is ScrollView scroll && (ReferenceEquals(scroll.contentContainer, element) || scroll.contentContainer.Contains(element))) return true;
            return false;
        }
        static void AuditText(TextElement text, List<LayoutIssue> results)
        {
            var content = text.contentRect;
            bool nowrap = text.resolvedStyle.whiteSpace == WhiteSpace.NoWrap;
            if (nowrap && text.ClassListContains(UIStyleClasses.TextEllipsis)) return;
            // AtMost clamps a no-wrap measurement to the supplied width and would hide overflow.
            Vector2 size = text.MeasureTextSize(text.text, nowrap ? 0 : content.width,
                nowrap ? VisualElement.MeasureMode.Undefined : VisualElement.MeasureMode.AtMost,
                0, VisualElement.MeasureMode.Undefined);
            bool overflow = nowrap ? size.x > content.width + Tolerance : size.y > content.height + Tolerance;
            if (overflow)
                Add(results, LayoutIssueKind.TextOverflow, text, text.worldBound, text.LocalToWorld(content));
        }
        static bool Contains(Rect container, Rect bounds)
            => bounds.xMin >= container.xMin - Tolerance && bounds.yMin >= container.yMin - Tolerance
                && bounds.xMax <= container.xMax + Tolerance && bounds.yMax <= container.yMax + Tolerance;
        static void Add(List<LayoutIssue> results, LayoutIssueKind kind, VisualElement element, Rect bounds, Rect container)
            => results.Add(new LayoutIssue { Kind = kind, ElementPath = ElementPath(element), Bounds = bounds, Container = container });
        internal static string ElementPath(VisualElement element)
        {
            var names = new List<string>();
            for (var current = element; current != null; current = current.parent)
                if (!string.IsNullOrEmpty(current.name)) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
