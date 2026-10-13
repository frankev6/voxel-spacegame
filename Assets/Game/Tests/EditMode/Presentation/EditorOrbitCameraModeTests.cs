using Game.Presentation.Data;
using Game.Presentation.Services;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Fixture = Game.Tests.EditMode.Presentation.CameraRigServiceTests.Fixture;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>SPEC-62 CAM-009: editor orbit, bounded pan, input suppression, view keys and sphere framing.</summary>
    public sealed class EditorOrbitCameraModeTests
    {
        sealed class FrameInput : ICameraInput
        {
            public Vector2 OrbitDelta { get; set; }
            public Vector2 PanDelta { get; set; }
            public float ZoomSteps { get; set; }
            public bool RecenterPressed => false;
            public bool FocusPressed { get; set; }
            public bool ViewIsoPressed { get; set; }
            public bool ViewTopPressed { get; set; }
            public bool ViewFrontPressed { get; set; }
            public bool ViewSidePressed { get; set; }
            public void Sample() { }
            public void SetView(EditorView? view)
            {
                ViewIsoPressed = view == EditorView.Iso; ViewTopPressed = view == EditorView.Top;
                ViewFrontPressed = view == EditorView.Front; ViewSidePressed = view == EditorView.Side;
            }
        }

        static void Start(Fixture f, float3 center = default)
        {
            f.Rig.EnterEditorOrbit(center, 10, true);
            f.Rig.Tick(0);
        }
        static void Settle(Fixture f)
        {
            for (int i = 0; i < 30; i++) f.Rig.Tick(.1f);
        }
        static float Pitch(Fixture f) => math.degrees(math.asin(math.clamp(-f.Rig.Camera.transform.forward.y, -1, 1)));

        [Test] public void Enter_DefaultPose()
        {
            using var f = new Fixture(); var center = new float3(10, 4, -20);
            Start(f, center);
            Assert.AreEqual(CameraMode.EditorOrbit, f.Rig.Mode);
            Assert.AreEqual(center, f.Rig.Focus);
            Assert.AreEqual(30, f.Rig.CurrentDistance);
            Assert.AreEqual(45, f.Rig.YawDeg);
            Assert.AreEqual(30, Pitch(f), .001f);
            Assert.AreEqual(30, math.distance(center, f.Rig.Camera.transform.position), .0001f);
            Assert.AreEqual(15, f.Rig.Camera.transform.position.y - center.y, .0001f);
            var towardPivot = math.normalize(center - (float3)f.Rig.Camera.transform.position);
            Assert.Greater(math.dot(towardPivot, (float3)f.Rig.Camera.transform.forward), .99999f);
        }

        [Test] public void Orbit_PitchClamped()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input); Start(f);
            foreach (float direction in new[] { 1f, -1f })
            {
                input.OrbitDelta = new Vector2(1000, direction * 100000);
                for (int i = 0; i < 20; i++)
                {
                    f.Rig.Tick(.1f);
                    Assert.That(Pitch(f), Is.InRange(-89.01f, 89.01f));
                }
                input.OrbitDelta = Vector2.zero; Settle(f);
                Assert.AreEqual(direction > 0 ? -89 : 89, Pitch(f), .01f);
            }
        }

        [Test] public void Pan_PivotClamped()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input);
            var center = new float3(10, 0, -20); Start(f, center);
            input.PanDelta = new Vector2(8, -6);
            float3 right = f.Rig.Camera.transform.right, up = f.Rig.Camera.transform.up;
            float3 delta = (-right * 8 + up * 6) * 30 * f.Config.EditorPanPerPixelPerMeter;
            f.Rig.Tick(f.Config.EditorHalfLife);
            Assert.Less(math.distance(center + delta * .5f, f.Rig.Focus), .0001f, "Pivot damping moves half way in one half-life.");
            input.PanDelta = new Vector2(1000000, -1000000);
            float extent = 10 + f.Config.EditorPivotBoundsPad;
            for (int i = 0; i < 30; i++)
            {
                f.Rig.Tick(.1f);
                Assert.IsTrue(math.all(math.abs(f.Rig.Focus - center) <= extent + .0001f));
            }
            input.PanDelta = Vector2.zero; Settle(f);
            Assert.AreEqual(extent, math.cmax(math.abs(f.Rig.Focus - center)), .001f);
        }

        [Test] public void Zoom_Suppressed()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input); Start(f);
            f.Rig.SetEditorSuppression(zoom: true, orbit: true);
            input.ZoomSteps = 1; input.OrbitDelta = new Vector2(4, 4);
            for (int i = 0; i < 10; i++) f.Rig.Tick(.1f);
            Assert.AreEqual(30, f.Rig.CurrentDistance);
            Assert.AreEqual(45, f.Rig.YawDeg); Assert.AreEqual(30, Pitch(f), .001f);
            f.Rig.SetMode(CameraMode.EditorOrbit, true); f.Rig.Tick(.1f);
            Assert.Less(f.Rig.CurrentDistance, 30, "SetMode clears zoom suppression.");
            Assert.Greater(f.Rig.YawDeg, 45, "SetMode also clears orbit suppression.");
            Assert.Less(Pitch(f), 30);
            input.ZoomSteps = 0; input.OrbitDelta = Vector2.zero; Settle(f);
            Assert.AreEqual(30 * f.Config.ZoomStepFactor, f.Rig.CurrentDistance, .001f);
        }

        [Test] public void SnapViews_Angles()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input); Start(f);
            var views = new[]
            {
                (View: EditorView.Front, Yaw: 0f, Pitch: 0f),
                (View: EditorView.Side, Yaw: 90f, Pitch: 0f),
                (View: EditorView.Iso, Yaw: 45f, Pitch: 30f),
                (View: EditorView.Top, Yaw: 45f, Pitch: 89f)
            };
            foreach (var row in views)
            {
                f.Rig.SnapView(row.View); Settle(f);
                Assert.AreEqual(row.Yaw, f.Rig.YawDeg, .001f, row.View.ToString());
                Assert.AreEqual(row.Pitch, Pitch(f), .01f, row.View.ToString());
            }
            foreach (var row in views)
            {
                input.SetView(row.View); f.Rig.Tick(1f / 60); input.SetView(null); Settle(f);
                Assert.AreEqual(row.Yaw, f.Rig.YawDeg, .001f, "View key " + row.View);
                Assert.AreEqual(row.Pitch, Pitch(f), .01f, "View key " + row.View);
            }
        }

        [Test] public void FocusOn_SphereFitsFov()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input); Start(f);
            var center = new float3(3, 2, 1); const float radius = 5;
            f.Rig.FocusOn(center, radius); Settle(f);
            Assert.Less(math.distance(center, f.Rig.Focus), .0001f);
            float expected = radius * f.Config.EditorFocusPadding / math.sin(math.radians(f.Config.VerticalFovDeg * .5f));
            Assert.AreEqual(expected, f.Rig.CurrentDistance, .001f);
            var camera = f.Rig.Camera;
            float distance = math.distance(center, camera.transform.position);
            Assert.LessOrEqual(math.asin(radius / distance), math.radians(camera.fieldOfView * .5f));
            // Sphere silhouettes reach their vertical extrema at the tangent points, not the world-space poles.
            float towardCamera = radius * radius / distance;
            float vertical = radius * math.sqrt(1 - radius * radius / (distance * distance));
            float3 tangentCenter = center - (float3)camera.transform.forward * towardCamera;
            foreach (float sign in new[] { -1f, 1f })
            {
                var viewport = camera.WorldToViewportPoint(tangentCenter + (float3)camera.transform.up * vertical * sign);
                Assert.That(viewport.y, Is.InRange(0f, 1f)); Assert.Greater(viewport.z, 0);
            }
            input.FocusPressed = true; f.Rig.Tick(1f / 60); input.FocusPressed = false; Settle(f);
            Assert.Less(math.length(f.Rig.Focus), .0001f, "Focus key frames the configured editor bounds.");
            Assert.AreEqual(10 * f.Config.EditorFocusPadding / math.sin(math.radians(f.Config.VerticalFovDeg * .5f)), f.Rig.CurrentDistance, .001f);
        }
    }
}
