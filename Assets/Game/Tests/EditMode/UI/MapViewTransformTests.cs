using Game.UI.Data;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.UI
{
    /// <summary>CAM-010: six specified map pan/zoom tests (SPEC-62 §4.3, Q17 pixel-precision limit).</summary>
    public sealed class MapViewTransformTests
    {
        static MapViewTransform View(double2 center, double unitsPerPixel = 2)
            => new MapViewTransform
            {
                Center = center, UnitsPerPixel = unitsPerPixel,
                MinUnitsPerPixel = .125, MaxUnitsPerPixel = 1000000000,
                ViewSize = new float2(1024, 768)
            };

        [Test]
        public void RoundTrip_Exact()
        {
            // Binary pixel-representable offsets are chosen before calling either transform (Q17).
            foreach (var center in new[] { double2.zero, new double2(1e6, -1e6), new double2(1e12 - 1024, -1e12 + 1024) })
                foreach (double scale in new[] { .125, 1d, 1024d })
                {
                    var view = View(center, scale);
                    foreach (var offset in new[] { double2.zero, new double2(-.5, .25), new double2(1, -2), new double2(63, 31) })
                    {
                        var point = center + offset;
                        var result = view.ViewToMap(view.MapToView(point));
                        for (int axis = 0; axis < 2; axis++)
                            Assert.AreEqual(point[axis], result[axis], math.max(1d, math.abs(point[axis])) * 1e-9);
                    }
                    Assert.AreEqual(view.ViewSize * .5f, view.MapToView(center));
                }
            var orientation = View(0, 2);
            Assert.AreEqual(new float2(514, 381), orientation.MapToView(new double2(4, 6)));
        }

        [Test]
        public void ZoomAt_KeepsPointUnderCursor()
        {
            foreach (var center in new[] { new double2(200, -300), new double2(1e12, -1e12) })
            {
                var view = View(center, 2048);
                foreach (var cursor in new[] { new float2(512, 384), new float2(17.5f, 61.25f), new float2(998, 702) })
                {
                    var point = view.ViewToMap(cursor);
                    double before = view.UnitsPerPixel;
                    view.ZoomAt(cursor, 1.5f);
                    Assert.AreEqual(before * System.Math.Pow(.85, 1.5), view.UnitsPerPixel, before * 1e-12);
                    Assert.LessOrEqual(math.distance(cursor, view.MapToView(point)), .01f);
                    view.ZoomAt(cursor, -2);
                    Assert.LessOrEqual(math.distance(cursor, view.MapToView(point)), .01f);
                }
            }
        }

        [Test]
        public void Zoom_Clamped()
        {
            var view = View(new double2(20, 30));
            view.MinUnitsPerPixel = .5;
            view.MaxUnitsPerPixel = 8;
            var cursor = new float2(117, 611);
            var point = view.ViewToMap(cursor);
            view.ZoomAt(cursor, 1000);
            Assert.AreEqual(.5, view.UnitsPerPixel);
            Assert.LessOrEqual(math.distance(cursor, view.MapToView(point)), .01f);
            var centerAtLimit = view.Center;
            view.ZoomAt(cursor, 1);
            Assert.AreEqual(centerAtLimit, view.Center);
            view.ZoomAt(cursor, -1000);
            Assert.AreEqual(8, view.UnitsPerPixel);
            Assert.LessOrEqual(math.distance(cursor, view.MapToView(point)), .01f);
            centerAtLimit = view.Center;
            view.ZoomAt(cursor, -1);
            Assert.AreEqual(centerAtLimit, view.Center);
        }

        [Test]
        public void Pan_MovesOppositeToDrag()
        {
            var view = View(new double2(100, -50), 4);
            var point = new double2(80, -40);
            var before = view.MapToView(point);
            var delta = new float2(12, -7);
            view.Pan(delta);
            Assert.AreEqual(new double2(52, -78), view.Center);
            Assert.AreEqual(before + delta, view.MapToView(point));
            view.Pan(-delta);
            Assert.AreEqual(new double2(100, -50), view.Center);
        }

        [Test]
        public void Frame_FitsBoxWithPadding()
        {
            var view = View(new double2(-200, 100), 4);
            view.ViewSize = new float2(1000, 600);
            var min = new double2(-400, -100);
            var max = new double2(400, 100);
            view.Frame(min, max, 50);
            Assert.AreEqual(double2.zero, view.Center);
            Assert.AreEqual(800d / 900, view.UnitsPerPixel, 1e-12);
            var lowerLeft = view.MapToView(min);
            var upperRight = view.MapToView(max);
            Assert.GreaterOrEqual(lowerLeft.x, 50); Assert.LessOrEqual(upperRight.x, 950);
            Assert.GreaterOrEqual(upperRight.y, 50); Assert.LessOrEqual(lowerLeft.y, 550);

            view.Frame(new double2(-50, -400), new double2(50, 400), 50);
            Assert.AreEqual(800d / 500, view.UnitsPerPixel, 1e-12, "The larger vertical fit wins.");
            view.MinUnitsPerPixel = 2;
            view.Frame(min, max, 50);
            Assert.AreEqual(2, view.UnitsPerPixel);
            view.MaxUnitsPerPixel = 4;
            view.Frame(new double2(-10000), new double2(10000), 50);
            Assert.AreEqual(4, view.UnitsPerPixel);
        }

        [Test]
        public void ClampCenter_StaysInside()
        {
            var view = View(new double2(-50, 90), 4);
            var min = new double2(-10, -20);
            var max = new double2(30, 40);
            view.ClampCenter(min, max);
            Assert.AreEqual(new double2(-10, 40), view.Center);
            view.Center = new double2(7, -2);
            view.ClampCenter(min, max);
            Assert.AreEqual(new double2(7, -2), view.Center);
            Assert.AreEqual(4, view.UnitsPerPixel);
        }
    }
}
