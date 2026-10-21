using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>HUD redesign (2026-10-21): the reticle with its three fire-group ticks, the lead pip, and the hull (left), shield (right)
    /// and energy (below) arcs around the player's ship, drawn with the vector API so nothing sits in a corner. Colours are the NOCTURNE
    /// tokens (tokens.uss); the view repaints it when <see cref="HudViewModel.MarksVersion"/> or a fire group changes.</summary>
    public sealed class HudMarks : VisualElement
    {
        static readonly Color Hull = new(.93f, .9f, .855f), Shield = new(.247f, .847f, 1f), Energy = new(1f, .72f, .3f), Danger = new(1f, .36f, .48f),
            Accent = new(.224f, .835f, 1f), Lock = new(1f, .76f, .28f), Reticle = new(.86f, .95f, 1f);
        const float HullSpan = 70, ShieldSpan = 70, EnergySpan = 56;
        readonly HudViewModel model;
        public HudMarks(HudViewModel model)
        {
            this.model = model; name = "hud-marks"; pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            generateVisualContent += Draw;
        }
        /// <summary>Arc radius in panel pixels around the ship (used by the view to place the numbers beside the arcs).</summary>
        public static float ArcRadius(HudViewModel model, float height) => Mathf.Clamp(model.ShipRadiusFraction * height * .8f + 10, 30, height * .16f);
        void Draw(MeshGenerationContext context)
        {
            if (model == null || !model.HasShip) return;
            var size = contentRect.size;
            if (!(size.x > 0 && size.y > 0)) return;
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
            if (model.ShowShip) ShipArcs(painter, size);
            if (model.ShowLead) Lead(painter, Scale(model.LeadPoint, size));
            if (model.ShowReticle) Cursor(painter, Scale(model.ReticlePoint, size));
        }
        static Vector2 Scale(Vector2 fraction, Vector2 size) => new(fraction.x * size.x, fraction.y * size.y);
        void ShipArcs(Painter2D painter, Vector2 size)
        {
            Vector2 centre = Scale(model.ShipPoint, size);
            float radius = ArcRadius(model, size.y);
            // Angles are clockwise from +x with y down: 180 is the ship's left on screen, 0 its right, 90 below it. Each arc fills
            // upward from its lower end; a value under 30 % turns the hull arc red.
            if (model.HullArc > .01f)
                Arc(painter, centre, radius, 180 - HullSpan * .5f, HullSpan, model.HullFraction, model.HullFraction < .3f ? Danger : Hull, model.HullArc, 3, false);
            if (model.ShieldArc > .01f)
                Arc(painter, centre, radius, ShieldSpan * .5f, ShieldSpan, model.ShieldFraction, Shield, model.ShieldArc, 3, true);
            if (model.EnergyArc > .01f)
                Arc(painter, centre, radius + 6, 90 + EnergySpan * .5f, EnergySpan, model.EnergyFraction, Energy, model.EnergyArc, 2, true);
        }
        static void Arc(Painter2D painter, Vector2 centre, float radius, float start, float span, float fraction, Color color, float alpha, float width, bool counterClockwise)
        {
            var direction = counterClockwise ? ArcDirection.CounterClockwise : ArcDirection.Clockwise;
            float end = counterClockwise ? start - span : start + span;
            painter.lineWidth = width; painter.strokeColor = new Color(1, 1, 1, .16f * alpha);
            painter.BeginPath(); painter.Arc(centre, radius, Angle.Degrees(start), Angle.Degrees(end), direction); painter.Stroke();
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= .001f) return;
            float filled = counterClockwise ? start - span * fraction : start + span * fraction;
            painter.strokeColor = new Color(color.r, color.g, color.b, alpha);
            painter.BeginPath(); painter.Arc(centre, radius, Angle.Degrees(start), Angle.Degrees(filled), direction); painter.Stroke();
        }
        void Lead(Painter2D painter, Vector2 point)
        {
            // where the shots meet the target: a small amber diamond with a dot
            painter.lineWidth = 1.5f; painter.strokeColor = new Color(Lock.r, Lock.g, Lock.b, .95f);
            painter.BeginPath();
            painter.MoveTo(point + new Vector2(0, -6)); painter.LineTo(point + new Vector2(6, 0)); painter.LineTo(point + new Vector2(0, 6)); painter.LineTo(point + new Vector2(-6, 0));
            painter.ClosePath(); painter.Stroke();
            Dot(painter, point, 1.6f, Lock);
        }
        void Cursor(Painter2D painter, Vector2 point)
        {
            var color = model.Locked ? Lock : Reticle;
            painter.lineWidth = 1.5f; painter.strokeColor = new Color(color.r, color.g, color.b, .92f);
            painter.BeginPath(); painter.Arc(point, 10, Angle.Degrees(0), Angle.Degrees(360)); painter.Stroke();
            if (model.Locked)
            {   // four short spokes: auto-aim holds a ship
                for (int i = 0; i < 4; i++)
                {
                    var axis = i == 0 ? Vector2.up : i == 1 ? Vector2.down : i == 2 ? Vector2.left : Vector2.right;
                    painter.BeginPath(); painter.MoveTo(point + axis * 13); painter.LineTo(point + axis * 17); painter.Stroke();
                }
            }
            Dot(painter, point, 1.8f, color);
            // fire groups LMB / RMB / G, left to right: bright = ready, a partial bar = cooling, white = firing
            var groups = model.WeaponGroups;
            for (int i = 0; i < 3 && i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || group.WeaponCount <= 0) continue;
                Vector2 left = point + new Vector2(-17 + i * 12, 19), right = left + new Vector2(9, 0);
                painter.lineWidth = 2.5f; painter.lineCap = LineCap.Butt;
                painter.strokeColor = new Color(1, 1, 1, .2f);
                painter.BeginPath(); painter.MoveTo(left); painter.LineTo(right); painter.Stroke();
                float ready = Mathf.Clamp01(group.ReadyFraction);
                var tick = group.Firing ? Color.white : ready >= 1 ? Accent : new Color(Accent.r, Accent.g, Accent.b, .6f);
                if (ready > .01f)
                {
                    painter.strokeColor = tick;
                    painter.BeginPath(); painter.MoveTo(left); painter.LineTo(Vector2.Lerp(left, right, ready)); painter.Stroke();
                }
                painter.lineCap = LineCap.Round;
            }
        }
        static void Dot(Painter2D painter, Vector2 point, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath(); painter.Arc(point, radius, Angle.Degrees(0), Angle.Degrees(360)); painter.Fill();
        }
    }
}
