using System;
using Game.UI.Controls;
using Game.UI.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>Shared map-screen UI gestures; the canvas remains the sole pan/zoom transform owner.</summary>
    internal sealed class MapScreenInteraction
    {
        readonly MapCanvas canvas;
        readonly Func<MapData> data;
        readonly Action focus;
        double2 pendingCenter;
        bool pending, frame;
        internal MapScreenInteraction(VisualElement root, string key, MapCanvas canvas, Func<MapData> data, Action focus)
        {
            this.canvas = canvas; this.data = data; this.focus = focus;
            canvas.focusable = true;
            root.Q<Button>(key + "-zoom-in").clicked += () => Zoom(1);
            root.Q<Button>(key + "-zoom-out").clicked += () => Zoom(-1);
            root.RegisterCallback<KeyDownEvent>(Key);
            canvas.RegisterCallback<GeometryChangedEvent>(_ => ApplyFocus());
        }
        internal void Focus(double2 center, bool frameBounds = true)
        { pendingCenter = center; pending = true; frame = frameBounds; ApplyFocus(); }
        void ApplyFocus()
        {
            if (!pending || math.any(canvas.View.ViewSize <= 48)) return;
            var map = data();
            if (frame && map != null) canvas.Frame(map.BoundsMin, map.BoundsMax);
            var view = canvas.View; view.Center = pendingCenter; canvas.View = view; pending = false;
        }
        void Zoom(float steps) => canvas.ZoomAt(canvas.View.ViewSize * .5f, steps);
        void Key(KeyDownEvent e)
        {
            // These are focused UI gestures in Menu context, without adding a second InputAction map.
            switch (e.keyCode)
            {
                case KeyCode.LeftArrow: canvas.Pan(new float2(40, 0)); break;
                case KeyCode.RightArrow: canvas.Pan(new float2(-40, 0)); break;
                case KeyCode.UpArrow: canvas.Pan(new float2(0, 40)); break;
                case KeyCode.DownArrow: canvas.Pan(new float2(0, -40)); break;
                case KeyCode.Equals: case KeyCode.Plus: case KeyCode.KeypadPlus: Zoom(1); break;
                case KeyCode.Minus: case KeyCode.KeypadMinus: Zoom(-1); break;
                case KeyCode.Home: focus(); break;
                default: return;
            }
            e.StopPropagation();
        }
    }
}
