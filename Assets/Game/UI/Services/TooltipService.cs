using System;
using System.Collections.Generic;
using Game.UI.Data;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §5.7/6: one pooled, delayed tooltip above every other UI layer.</summary>
    public sealed class TooltipService
    {
        static readonly ProfilerMarker PositionMarker = new ProfilerMarker(ProfilerCategory.Scripts, "TooltipService.Position");
        readonly UIRoot root;
        readonly VisualElement tooltip, stats;
        readonly Label title, subtitle, body;
        readonly VisualElement[] rows = new VisualElement[8];
        readonly Label[] statLabels = new Label[8], statValues = new Label[8];
        readonly TooltipContent content = new TooltipContent();
        readonly Dictionary<VisualElement, TooltipManipulator> attachments = new Dictionary<VisualElement, TooltipManipulator>();
        readonly IVisualElementScheduledItem delayItem;
        readonly long delayMilliseconds;
        readonly CustomStyleProperty<float> offsetXProperty = new CustomStyleProperty<float>("--tooltip-offset-x");
        readonly CustomStyleProperty<float> offsetYProperty = new CustomStyleProperty<float>("--tooltip-offset-y");
        readonly CustomStyleProperty<float> marginProperty = new CustomStyleProperty<float>("--tooltip-edge-margin");
        VisualElement currentTarget;
        Action<TooltipContent> currentFill;
        Vector2 pointerPosition;
        bool visible, screenTooltip, disposed;
        uint requestVersion;

        internal TooltipService(UIRoot root, UIConfig config)
        {
            this.root = root;
            if (config.TooltipUxml == null || !float.IsFinite(config.TooltipDelaySeconds) || config.TooltipDelaySeconds < 0
                || config.TooltipDelaySeconds >= long.MaxValue / 1000d)
                throw new InvalidOperationException("UIConfig requires tooltip UXML and a finite nonnegative tooltip delay in scheduler range");
            delayMilliseconds = (long)(config.TooltipDelaySeconds * 1000);
            var tree = config.TooltipUxml.Instantiate();
            tooltip = Require<VisualElement>(tree, "tooltip");
            title = Require<Label>(tooltip, "tooltip-title");
            subtitle = Require<Label>(tooltip, "tooltip-subtitle");
            body = Require<Label>(tooltip, "tooltip-body");
            stats = Require<VisualElement>(tooltip, "tooltip-stats");
            for (int i = 0; i < rows.Length; i++)
            {
                string stem = "tooltip-stat-" + i;
                var row = new VisualElement { name = stem, pickingMode = PickingMode.Ignore };
                row.AddToClassList(UIStyleClasses.TooltipStatRow); row.AddToClassList(UIStyleClasses.IsHidden);
                var label = new Label { name = stem + "-label", pickingMode = PickingMode.Ignore };
                label.AddToClassList(UIStyleClasses.TooltipStatLabel);
                var value = new Label { name = stem + "-value", pickingMode = PickingMode.Ignore };
                value.AddToClassList(UIStyleClasses.TooltipStatValue);
                row.Add(label); row.Add(value); stats.Add(row);
                rows[i] = row; statLabels[i] = label; statValues[i] = value;
            }
            tooltip.AddToClassList(UIStyleClasses.IsHidden);
            root.GetLayer(UILayer.Tooltip).Add(tooltip);
            delayItem = tooltip.schedule.Execute(OnDelay); delayItem.Pause();
            tooltip.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            tooltip.RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            tooltip.RegisterCallback<DetachFromPanelEvent>(OnTooltipDetached);
            root.RootElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root.Navigator.StackChanged += Hide;
        }
        static T Require<T>(VisualElement parent, string name) where T : VisualElement
            => parent.Q<T>(name) ?? throw new InvalidOperationException("Tooltip UXML missing '" + name + "'");
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(TooltipService)); }
        public bool IsVisible => visible;
        public void Attach(VisualElement target, Action<TooltipContent> fill)
        {
            CheckAlive();
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (fill == null) throw new ArgumentNullException(nameof(fill));
            Detach(target);
            var manipulator = new TooltipManipulator(this, fill);
            attachments.Add(target, manipulator); target.AddManipulator(manipulator);
        }
        public void Detach(VisualElement target)
        {
            CheckAlive();
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!attachments.TryGetValue(target, out var manipulator)) return;
            Cancel(target); target.RemoveManipulator(manipulator); attachments.Remove(target);
        }
        internal void Enter(VisualElement target, Action<TooltipContent> fill, Vector2 position)
        {
            if (disposed) return;
            Hide(); currentTarget = target; currentFill = fill; pointerPosition = position;
            delayItem.ExecuteLater(delayMilliseconds);
        }
        internal void Move(VisualElement target, Vector2 position)
        {
            if (disposed || !ReferenceEquals(currentTarget, target)) return;
            pointerPosition = position;
            if (visible) Position();
        }
        internal void Cancel(VisualElement target)
        { if (ReferenceEquals(currentTarget, target)) Hide(); }
        void OnDelay()
        {
            delayItem.Pause();
            if (disposed) return;
            if (currentTarget == null || currentTarget.panel == null || currentTarget.panel != tooltip.panel) { Hide(); return; }
            ShowContent();
        }
        void ShowContent()
        {
            var fill = currentFill;
            uint version = requestVersion;
            content.Clear();
            try { fill(content); }
            catch { Hide(); throw; }
            // Providers may navigate/detach while filling; cancellation must win over that stale result.
            if (disposed || requestVersion != version) return;
            SetText(title, content.Title); SetText(subtitle, content.Subtitle); SetText(body, content.Body);
            int count = Math.Min(rows.Length, content.Stats.Count);
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].EnableInClassList(UIStyleClasses.IsHidden, i >= count);
                TooltipStat stat = i < count ? content.Stats[i] : default;
                statLabels[i].text = stat.Label ?? ""; statValues[i].text = stat.Value ?? "";
                statValues[i].EnableInClassList(UIStyleClasses.TooltipStatValueBetter, stat.Sign > 0);
                statValues[i].EnableInClassList(UIStyleClasses.TooltipStatValueWorse, stat.Sign < 0);
            }
            stats.EnableInClassList(UIStyleClasses.IsHidden, count == 0);
            visible = true; tooltip.RemoveFromClassList(UIStyleClasses.IsHidden); Position();
        }
        static void SetText(Label label, string text)
        { label.text = text ?? ""; label.EnableInClassList(UIStyleClasses.IsHidden, string.IsNullOrEmpty(text)); }
        public void ShowAtScreen(Action<TooltipContent> fill, Vector2 screenPosBottomLeft)
        {
            CheckAlive();
            if (fill == null) throw new ArgumentNullException(nameof(fill));
            if (!float.IsFinite(screenPosBottomLeft.x) || !float.IsFinite(screenPosBottomLeft.y))
                throw new ArgumentException("Tooltip position must be finite", nameof(screenPosBottomLeft));
            Hide();
            var panel = root.Panel;
            if (panel == null) return;
            // SPEC-60 Q23: use the actual panel's height, matching UIRoot's world-pointer gate.
            // Screen.height can describe the Editor window rather than the game panel in ECS callbacks.
            pointerPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosBottomLeft);
            pointerPosition.y = panel.visualTree.layout.height - pointerPosition.y;
            screenTooltip = true; currentFill = fill; ShowContent();
        }
        public void HideScreenTooltip()
        { CheckAlive(); if (screenTooltip) Hide(); }
        void Hide()
        {
            requestVersion++;
            visible = false; screenTooltip = false; currentTarget = null; currentFill = null;
            delayItem.Pause(); tooltip.AddToClassList(UIStyleClasses.IsHidden); content.Clear();
        }
        void OnGeometryChanged(GeometryChangedEvent evt) { if (visible) Position(); }
        void OnCustomStyleResolved(CustomStyleResolvedEvent evt) { if (visible) Position(); }
        void OnTooltipDetached(DetachFromPanelEvent evt) => Hide();
        float Read(CustomStyleProperty<float> property, float fallback)
            => tooltip.customStyle.TryGetValue(property, out float value) && float.IsFinite(value) && value >= 0 ? value : fallback;
        void Position()
        {
            using (PositionMarker.Auto())
            {
                var layer = tooltip.parent;
                if (layer == null) return;
                Vector2 pointer = layer.WorldToLocal(pointerPosition);
                float width = tooltip.layout.width, height = tooltip.layout.height;
                float panelWidth = layer.layout.width, panelHeight = layer.layout.height;
                if (!float.IsFinite(width) || !float.IsFinite(height) || !float.IsFinite(panelWidth) || !float.IsFinite(panelHeight)) return;
                float offsetX = Read(offsetXProperty, 16), offsetY = Read(offsetYProperty, 20), margin = Read(marginProperty, 8);
                float x = pointer.x + offsetX, y = pointer.y + offsetY;
                if (x + width > panelWidth - margin) x = pointer.x - offsetX - width;
                if (y + height > panelHeight - margin) y = pointer.y - offsetY - height;
                x = Mathf.Clamp(x, margin, Mathf.Max(margin, panelWidth - width - margin));
                y = Mathf.Clamp(y, margin, Mathf.Max(margin, panelHeight - height - margin));
                if (tooltip.style.left.value.value != x) tooltip.style.left = x;
                if (tooltip.style.top.value.value != y) tooltip.style.top = y;
            }
        }
        internal void Tick()
        {
            if (!disposed && currentTarget != null && (currentTarget.panel == null || currentTarget.panel != tooltip.panel)) Hide();
        }
        internal void Dispose()
        {
            if (disposed) return;
            Hide(); disposed = true;
            root.Navigator.StackChanged -= Hide;
            root.RootElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            tooltip.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            tooltip.UnregisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            tooltip.UnregisterCallback<DetachFromPanelEvent>(OnTooltipDetached);
            foreach (var pair in attachments) pair.Key.RemoveManipulator(pair.Value);
            attachments.Clear(); tooltip.RemoveFromHierarchy();
        }
    }
}
