using System;
using System.Collections.Generic;
using Game.UI.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>SPEC-60 §5.12. WCAG sRGB luminance over the resolved, composited UI background.</summary>
    public static class ContrastAudit
    {
        static readonly CustomStyleProperty<Color> BackgroundToken = new CustomStyleProperty<Color>("--color-bg");
        public static void Run(VisualElement root, List<ContrastIssue> results)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            var ancestors = new List<VisualElement>();
            for (var current = root.parent; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.opacity <= 0) return;
                ancestors.Add(current);
            }
            Color background = ResolveBackground(root);
            for (int i = ancestors.Count - 1; i >= 0; i--)
                background = Over(ancestors[i].resolvedStyle.backgroundColor, background);
            Visit(root, background, results);
        }
        static Color ResolveBackground(VisualElement root)
        {
            for (var current = root; current != null; current = current.parent)
                if (current.customStyle.TryGetValue(BackgroundToken, out Color value))
                {
                    if (value.a < 1) throw new InvalidOperationException("ContrastAudit requires an opaque --color-bg token.");
                    return value;
                }
            throw new InvalidOperationException("ContrastAudit requires the resolved --color-bg theme token; attach the themed panel and wait for layout.");
        }
        static void Visit(VisualElement element, Color inheritedBackground, List<ContrastIssue> results)
        {
            var style = element.resolvedStyle;
            if (style.display == DisplayStyle.None || style.opacity <= 0) return;
            Color background = Over(style.backgroundColor, inheritedBackground);
            if (element is TextElement text && !string.IsNullOrEmpty(text.text) && style.visibility == Visibility.Visible)
            {
                Color foreground = style.color;
                Color displayed = Over(foreground, background);
                float a = Luminance(displayed), b = Luminance(background);
                float ratio = (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
                bool bold = style.unityFontStyleAndWeight == FontStyle.Bold || style.unityFontStyleAndWeight == FontStyle.BoldAndItalic;
                float required = style.fontSize >= 24 || (style.fontSize >= 18.66f && bold) ? 3 : 4.5f;
                if (ratio < required)
                    results.Add(new ContrastIssue
                    {
                        ElementPath = UILayoutAudit.ElementPath(element), Foreground = foreground,
                        Background = background, Ratio = ratio, Required = required
                    });
            }
            // Visibility is inherited but a child can explicitly restore Visible; don't prune hidden parents.
            for (int i = 0; i < element.hierarchy.childCount; i++) Visit(element.hierarchy[i], background, results);
        }
        static Color Over(Color foreground, Color background)
        {
            float alpha = Mathf.Clamp01(foreground.a);
            return new Color(foreground.r * alpha + background.r * (1 - alpha),
                foreground.g * alpha + background.g * (1 - alpha),
                foreground.b * alpha + background.b * (1 - alpha), 1);
        }
        static float Luminance(Color srgb) => .2126f * Linear(srgb.r) + .7152f * Linear(srgb.g) + .0722f * Linear(srgb.b);
        // https://www.w3.org/TR/WCAG22/#dfn-relative-luminance (same sRGB definition as updated WCAG 2.1).
        static float Linear(float value)
        {
            value = Mathf.Clamp01(value);
            return value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
        }
    }
}
