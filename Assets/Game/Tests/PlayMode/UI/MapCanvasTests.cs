using System;
using System.Collections;
using Game.UI.Controls;
using Game.UI.Data;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>HUD-015's five functional cases on a real rendered UI Toolkit panel.</summary>
    public sealed class MapCanvasTests
    {
        internal sealed class Fixture : IDisposable
        {
            internal readonly UITestPanel Panel = new(800, 600);
            internal readonly MapCanvas Canvas = new();
            internal readonly MapData Data = new();
            internal Fixture(int points = 3)
            {
                Canvas.style.width = 800; Canvas.style.height = 600; Canvas.style.flexGrow = 0;
                Panel.Root.Add(Canvas);
                for (int i = 0; i < points; i++) Data.Points.Add(new MapPoint {
                    Pos = points == 3 ? new double2(i * 8, 0) : new double2(i % 32 * 16 - 248, i / 32 * 12 - 186),
                    Name = "Point " + i, RadiusPx = 3, Flags = MapPointFlags.Known | (i < 64 ? MapPointFlags.Visited : 0),
                    ColorIndex = (byte)(i % 8) });
                for (int i = 0; i < points * 4; i++) Data.Lines.Add(new MapLine { A = i % points, B = (i + 1) % points, Known = true, Gated = i % 8 == 0 });
                Data.Rings.Add(new MapRing { Center = default, Radius = 80 });
                Data.Markers.Add(new MapMarker { Pos = new double2(-80, 0), Kind = MapMarkerKind.Player, HeadingDeg = 45 });
                Canvas.SetData(Data, 1);
            }
            internal IEnumerator Ready()
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 2;
                while (Canvas.TessellationCount == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.Greater(Canvas.TessellationCount, 0, "Actual map geometry must be rendered.");
                yield return null;
            }
            internal void Pointer(EventType kind, Vector2 local)
            {
                var source = new Event { type = kind, button = 0, mousePosition = Canvas.LocalToWorld(local) };
                if (kind == EventType.MouseDown) { using var e = PointerDownEvent.GetPooled(source); e.target = Canvas; Canvas.SendEvent(e); }
                else if (kind == EventType.MouseUp) { using var e = PointerUpEvent.GetPooled(source); e.target = Canvas; Canvas.SendEvent(e); }
                else { using var e = PointerMoveEvent.GetPooled(source); e.target = Canvas; Canvas.SendEvent(e); }
            }
            public void Dispose() => Panel.Dispose();
        }
        [UnityTest] public IEnumerator Pan_NoTessellation()
        {
            using var f = new Fixture(); yield return f.Ready();
            int count = f.Canvas.TessellationCount; float2 before = f.Canvas.View.MapToView(default);
            f.Canvas.Pan(new float2(40, 15)); yield return null; yield return null;
            Assert.AreEqual(count, f.Canvas.TessellationCount);
            Assert.Less(math.distance(before + new float2(40, 15), f.Canvas.View.MapToView(default)), .001f);
        }
        [UnityTest] public IEnumerator SmallZoom_NoTessellation()
        {
            using var f = new Fixture(); yield return f.Ready(); int count = f.Canvas.TessellationCount;
            float2 cursor = new(200, 150); double2 anchor = f.Canvas.View.ViewToMap(cursor);
            f.Canvas.ZoomAt(cursor, .25f); yield return null; yield return null;
            Assert.AreEqual(count, f.Canvas.TessellationCount); Assert.Less(math.distance(anchor, f.Canvas.View.ViewToMap(cursor)), 1e-9);
        }
        [UnityTest] public IEnumerator LargeZoom_Tessellates()
        {
            using var f = new Fixture(); yield return f.Ready(); int count = f.Canvas.TessellationCount;
            f.Canvas.ZoomAt(new float2(200, 150), 1); yield return null; yield return null;
            Assert.Greater(f.Canvas.TessellationCount, count);
            int drawn = f.Canvas.TessellationCount; f.Canvas.Pan(new float2(5, 8)); yield return null;
            Assert.AreEqual(drawn, f.Canvas.TessellationCount, "A rebuilt zoom establishes the new pan reference.");
        }
        [UnityTest] public IEnumerator Click_SelectsNearestWithin12px()
        {
            using var f = new Fixture(); yield return f.Ready(); int selected = -1; f.Canvas.PointClicked += i => selected = i;
            f.Pointer(EventType.MouseDown, new Vector2(409, 300)); f.Pointer(EventType.MouseUp, new Vector2(409, 300));
            Assert.AreEqual(1, selected, "Select nearest point rather than first candidate.");
            selected = -1; f.Pointer(EventType.MouseDown, new Vector2(400, 313)); f.Pointer(EventType.MouseUp, new Vector2(400, 313));
            Assert.AreEqual(-1, selected, "Outside 12px must not select.");
        }
        [UnityTest] public IEnumerator Drag_DoesNotSelect()
        {
            using var f = new Fixture(); yield return f.Ready(); int calls = 0; f.Canvas.PointClicked += _ => calls++;
            f.Pointer(EventType.MouseDown, new Vector2(400, 300)); f.Pointer(EventType.MouseDrag, new Vector2(404, 300));
            f.Pointer(EventType.MouseDrag, new Vector2(400, 300)); f.Pointer(EventType.MouseUp, new Vector2(400, 300));
            Assert.AreEqual(0, calls, "Returning to the pressed position does not turn a drag into a click.");
        }
    }
}
