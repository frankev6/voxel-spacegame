using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    /// <summary>SPEC-61 §5.7: retained map geometry; pan and small zoom change transforms only.</summary>
    [UxmlElement]
    public partial class MapCanvas : VisualElement
    {
        static readonly ProfilerMarker TessellateMarker = new(ProfilerCategory.Scripts, "MapCanvas.Tessellate");
        readonly VisualElement content;
        readonly Label[] labels = new Label[64];
        readonly Color[] colors = new Color[8];
        readonly Dictionary<string, Color> tokenColors = new(StringComparer.Ordinal);
        MapData data;
        int version, pointer = -1;
        bool hasVersion, hasGeometry, dragged, styleResolved;
        Vector2 pressed, previous;
        MapViewTransform view = new() { UnitsPerPixel = 1, MinUnitsPerPixel = 1e-6, MaxUnitsPerPixel = 1e12 };
        MapViewTransform tessellated;
        Color accent, muted;
        public event Action<int> PointClicked;
        internal int TessellationCount { get; private set; }

        public MapCanvas()
        {
            AddToClassList("maps-canvas"); style.overflow = Overflow.Hidden;
            content = new VisualElement { name = "map-content", pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
            content.style.position = Position.Absolute;
            content.style.left = content.style.top = 0;
            content.style.width = content.style.height = Length.Percent(100);
            content.style.transformOrigin = new TransformOrigin(0, 0);
            Add(content);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = new Label { pickingMode = PickingMode.Ignore };
                labels[i].AddToClassList("maps-label"); labels[i].style.position = Position.Absolute;
                labels[i].style.left = labels[i].style.top = 0; labels[i].style.display = DisplayStyle.None;
                content.Add(labels[i]);
            }
            content.generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => { styleResolved = true; ResolveColors(); Invalidate(); });
            RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (e.newRect.size == e.oldRect.size) return;
                view.ViewSize = new float2(e.newRect.width, e.newRect.height); Invalidate();
            });
            RegisterCallback<PointerDownEvent>(Down);
            RegisterCallback<PointerMoveEvent>(Move);
            RegisterCallback<PointerUpEvent>(Up);
            RegisterCallback<PointerCaptureOutEvent>(_ => { pointer = -1; });
            RegisterCallback<DetachFromPanelEvent>(_ => { pointer = -1; styleResolved = false; });
            RegisterCallback<WheelEvent>(e => { ZoomAt(e.localMousePosition, -e.delta.y); e.StopPropagation(); });
        }

        public MapViewTransform View
        {
            get => view;
            set
            {
                if (!math.all(math.isfinite(value.Center)) || !double.IsFinite(value.UnitsPerPixel) || value.UnitsPerPixel <= 0 ||
                    !double.IsFinite(value.MinUnitsPerPixel) || value.MinUnitsPerPixel <= 0 ||
                    !double.IsFinite(value.MaxUnitsPerPixel) || value.MaxUnitsPerPixel < value.MinUnitsPerPixel)
                    throw new ArgumentOutOfRangeException(nameof(value));
                value.ViewSize = view.ViewSize; value.UnitsPerPixel = math.clamp(value.UnitsPerPixel, value.MinUnitsPerPixel, value.MaxUnitsPerPixel);
                view = value; UpdateTransform();
            }
        }
        public void SetData(MapData value, int dataVersion)
        {
            if (ReferenceEquals(data, value) && hasVersion && version == dataVersion) return;
            data = value; version = dataVersion; hasVersion = true; ResolveColors(); Invalidate();
        }
        public void Pan(float2 delta)
        {
            if (!math.all(math.isfinite(delta))) return;
            view.Pan(delta); UpdateTransform();
        }
        public void ZoomAt(float2 position, float steps)
        {
            if (!math.all(math.isfinite(position)) || !math.isfinite(steps)) return;
            view.ZoomAt(position, steps); UpdateTransform();
        }
        public void Frame(double2 min, double2 max, float paddingPx = 24)
        {
            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)) || math.any(max < min) ||
                paddingPx < 0 || !math.isfinite(paddingPx) || math.any(view.ViewSize <= 2 * paddingPx)) return;
            view.Frame(min, max, paddingPx); UpdateTransform();
        }
        void Invalidate()
        {
            hasGeometry = false; tessellated = view;
            content.style.translate = new Translate(0, 0); content.style.scale = new Scale(Vector3.one);
            UpdateLabels(); content.MarkDirtyRepaint();
        }
        void UpdateTransform()
        {
            if (!hasGeometry || Math.Abs(Math.Log(view.UnitsPerPixel / tessellated.UnitsPerPixel)) > Math.Log(1.1))
            { Invalidate(); return; }
            float scale = (float)(tessellated.UnitsPerPixel / view.UnitsPerPixel);
            float2 translation = view.MapToView(tessellated.Center) - tessellated.ViewSize * (.5f * scale);
            content.style.scale = new Scale(new Vector3(scale, scale, 1));
            content.style.translate = new Translate(translation.x, translation.y);
            UpdateLabels();
        }
        void Down(PointerDownEvent e)
        {
            if (e.button != 0 || pointer >= 0) return;
            pointer = e.pointerId; pressed = previous = (Vector2)e.localPosition; dragged = false;
            this.CapturePointer(pointer); e.StopPropagation();
        }
        void Move(PointerMoveEvent e)
        {
            if (pointer != e.pointerId) return;
            Vector2 position = e.localPosition;
            if (!dragged && (position - pressed).sqrMagnitude >= 16) dragged = true;
            if (dragged) Pan(position - previous);
            // Before the threshold is crossed retain the down position, so the first pan includes it.
            if (dragged) previous = position;
            e.StopPropagation();
        }
        void Up(PointerUpEvent e)
        {
            if (pointer != e.pointerId || e.button != 0) return;
            bool select = !dragged && ((Vector2)e.localPosition - pressed).sqrMagnitude < 16;
            int released = pointer; pointer = -1; this.ReleasePointer(released);
            if (select)
            {
                int index = Nearest(e.localPosition);
                if (index >= 0) PointClicked?.Invoke(index);
            }
            e.StopPropagation();
        }
        int Nearest(Vector2 position)
        {
            int nearest = -1; float distance = 144;
            if (data == null) return nearest;
            for (int i = 0; i < data.Points.Count; i++)
            {
                float squared = math.lengthsq(view.MapToView(data.Points[i].Pos) - new float2(position.x, position.y));
                if (squared <= distance && (nearest < 0 || squared < distance)) { nearest = i; distance = squared; }
            }
            return nearest;
        }
        void ResolveColors()
        {
            for (int i = 0; i < colors.Length; i++) customStyle.TryGetValue(new CustomStyleProperty<Color>("--maps-color-" + i), out colors[i]);
            customStyle.TryGetValue(new CustomStyleProperty<Color>("--color-accent"), out accent);
            customStyle.TryGetValue(new CustomStyleProperty<Color>("--color-text-muted"), out muted);
            tokenColors.Clear();
            if (data == null) return;
            foreach (var point in data.Points)
            {
                string token = point.ColorToken;
                if (string.IsNullOrEmpty(token) || tokenColors.ContainsKey(token)) continue;
                if (tokenColors.Count == 64) { GameLog.WarnOnce("MapCanvas.ColorLimit", "UI", "Map has more than 64 distinct colour tokens; remaining tokens use the default."); break; }
                if (!customStyle.TryGetValue(new CustomStyleProperty<Color>(token), out var color))
                {
                    // Screen OnPush supplies data after attachment but before the first style pass.
                    if (styleResolved && panel != null) GameLog.WarnOnce("MapCanvas.Color." + token, "UI", "Unknown map colour token: " + token);
                    color = colors[0];
                }
                tokenColors.Add(token, color);
            }
        }
        Color PointColor(MapPoint point)
        {
            if ((point.Flags & MapPointFlags.Selected) != 0) return accent;
            if ((point.Flags & MapPointFlags.Known) == 0) return muted;
            if (!string.IsNullOrEmpty(point.ColorToken)) return tokenColors.TryGetValue(point.ColorToken, out var color) ? color : colors[0];
            return colors[Math.Min(point.ColorIndex, (byte)7)];
        }
        void Draw(MeshGenerationContext context)
        {
            using var sample = TessellateMarker.Auto();
            hasGeometry = true; TessellationCount++;
            var painter = context.painter2D;
            if (data == null) return;
            painter.lineWidth = 2;
            foreach (var line in data.Lines)
            {
                if (!line.Known || line.A < 0 || line.B < 0 || line.A >= data.Points.Count || line.B >= data.Points.Count) continue;
                var a = data.Points[line.A]; var b = data.Points[line.B];
                painter.strokeColor = line.Route ? accent : colors[0];
                Line(painter, Pixel(a.Pos), Pixel(b.Pos), line.Gated);
            }
            painter.strokeColor = muted;
            foreach (var ring in data.Rings)
            {
                if (!double.IsFinite(ring.Radius) || ring.Radius <= 0) continue;
                painter.BeginPath();
                for (int i = 0; i <= 96; i++)
                {
                    double angle = i * (Math.PI * 2 / 96);
                    Vector2 position = Pixel(ring.Center + new double2(Math.Cos(angle), Math.Sin(angle)) * ring.Radius);
                    if (i == 0) painter.MoveTo(position); else painter.LineTo(position);
                }
                painter.Stroke();
            }
            foreach (var point in data.Points)
            {
                if (!math.all(math.isfinite(point.Pos)) || !math.isfinite(point.RadiusPx) || point.RadiusPx <= 0) continue;
                painter.fillColor = PointColor(point); painter.BeginPath(); painter.Arc(Pixel(point.Pos), point.RadiusPx, 0, 360); painter.Fill();
            }
            painter.strokeColor = accent; painter.fillColor = accent;
            foreach (var marker in data.Markers)
            {
                if (!math.all(math.isfinite(marker.Pos))) continue;
                Vector2 center = Pixel(marker.Pos);
                painter.BeginPath();
                if (marker.Kind == MapMarkerKind.Player)
                {
                    float angle = marker.HeadingDeg * Mathf.Deg2Rad;
                    Vector2 forward = new(Mathf.Sin(angle), -Mathf.Cos(angle)); Vector2 right = new(-forward.y, forward.x);
                    painter.MoveTo(center + forward * 9); painter.LineTo(center - forward * 6 + right * 6);
                    painter.LineTo(center - forward * 6 - right * 6); painter.ClosePath(); painter.Fill();
                }
                else
                {
                    float radius = marker.Kind == MapMarkerKind.Destination ? 10 : 7;
                    painter.MoveTo(center + Vector2.up * radius); painter.LineTo(center + Vector2.right * radius);
                    painter.LineTo(center + Vector2.down * radius); painter.LineTo(center + Vector2.left * radius);
                    painter.ClosePath(); painter.Stroke();
                }
            }
        }
        Vector2 Pixel(double2 point) { float2 p = tessellated.MapToView(point); return new Vector2(p.x, p.y); }
        static void Line(Painter2D painter, Vector2 a, Vector2 b, bool dashed)
        {
            if (!float.IsFinite(a.x) || !float.IsFinite(a.y) || !float.IsFinite(b.x) || !float.IsFinite(b.y)) return;
            painter.BeginPath();
            if (!dashed) { painter.MoveTo(a); painter.LineTo(b); }
            else
            {
                Vector2 delta = b - a; float length = delta.magnitude;
                if (!float.IsFinite(length) || length <= 0) return;
                Vector2 direction = delta / length;
                // Keep 6/4px dashes normally; bound extreme zoom-outside geometry and avoid a non-advancing float loop.
                int count = (int)Math.Min(Math.Ceiling(length / 10.0), 4096);
                float step = length > 40960 ? length / count : 10;
                for (int i = 0; i < count; i++)
                {
                    float d = i * step;
                    painter.MoveTo(a + direction * d); painter.LineTo(a + direction * Mathf.Min(d + step * .6f, length));
                }
            }
            painter.Stroke();
        }
        void UpdateLabels()
        {
            int used = 0;
            if (data != null)
                foreach (var point in data.Points)
                {
                    if (used == labels.Length) break;
                    if ((point.Flags & (MapPointFlags.Current | MapPointFlags.Destination | MapPointFlags.Selected | MapPointFlags.Visited)) == 0 || string.IsNullOrEmpty(point.Name)) continue;
                    float2 actual = view.MapToView(point.Pos);
                    if (!math.all(math.isfinite(actual)) || math.any(actual < 0) || math.any(actual > view.ViewSize)) continue;
                    var label = labels[used++]; Vector2 position = Pixel(point.Pos);
                    label.text = point.Name; label.style.translate = new Translate(position.x + point.RadiusPx + 5, position.y - 9);
                    label.style.display = DisplayStyle.Flex;
                }
            for (int i = used; i < labels.Length; i++) labels[i].style.display = DisplayStyle.None;
        }
    }
}
