using Game.Core;
using Game.Presentation.Data;
using Game.Presentation.Services;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Fixture = Game.Tests.EditMode.Presentation.CameraRigServiceTests.Fixture;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>CAM-005: flight pose, input bounds and frame-rate regressions.</summary>
    public sealed class FlightCameraModeTests
    {
        internal sealed class FrameInput : ICameraInput
        {
            public Vector2 OrbitDelta { get; set; }
            public Vector2 PanDelta => Vector2.zero;
            public float ZoomSteps { get; set; }
            public bool RecenterPressed { get; set; }
            public bool FocusPressed => false;
            public bool ViewIsoPressed => false;
            public bool ViewTopPressed => false;
            public bool ViewFrontPressed => false;
            public bool ViewSidePressed => false;
            public void Sample() { }
        }
        static void Start(Fixture f, float3 velocity = default)
        {
            f.Rig.SetMode(CameraMode.Flight, true);
            f.Rig.SetTarget(float3.zero, quaternion.identity, velocity, 10, true);
            f.Rig.Tick(0);
        }
        static float Pitch(Fixture f) => math.degrees(math.asin(-f.Rig.Camera.transform.forward.y));

        [Test] public void Default_PitchIs55_NotEdgeOn()
        {
            using var f = new Fixture(); Start(f);
            Assert.AreEqual(55, Pitch(f), 1e-4f); Assert.AreEqual(150, f.Rig.CurrentDistance);
            Assert.Less(f.Rig.Camera.transform.position.z, 0);
            Assert.AreEqual(150, math.distance(f.Rig.Focus, f.Rig.Camera.transform.position), 1e-4f);
        }
        [Test] public void FuzzOrbitInput_1000Frames_PitchInRange()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input); Start(f);
            var rng = RngStreams.For(62, RngPurpose.Test, 5);
            for (int frame = 0; frame < 1000; frame++)
            {
                input.OrbitDelta = rng.NextFloat2(new float2(-3000), new float2(3000));
                f.Rig.Tick(1f / 60);
                Assert.That(Pitch(f), Is.InRange(35f - 1e-4f, 80f + 1e-4f), "Frame " + frame);
            }
        }
        [Test] public void Zoom_ClampedToRange()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input); Start(f);
            foreach (float steps in new[] { 1000f, -1000f })
            {
                input.ZoomSteps = steps; f.Rig.Tick(1f / 60); input.ZoomSteps = 0;
                for (int i = 0; i < 240; i++) { f.Rig.Tick(1f / 60); Assert.That(f.Rig.CurrentDistance, Is.InRange(25f, 400f)); }
                Assert.AreEqual(steps > 0 ? 25 : 400, f.Rig.CurrentDistance, .001f);
            }
        }
        [Test] public void Recenter_ResetsOffsetAndPitch()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input); Start(f);
            input.OrbitDelta = new Vector2(360, 100); f.Rig.Tick(1f / 60); input.OrbitDelta = Vector2.zero;
            for (int i = 0; i < 120; i++) f.Rig.Tick(1f / 60);
            Assert.Greater(math.abs(f.Rig.YawDeg), 80); Assert.Less(Pitch(f), 40);
            input.RecenterPressed = true; f.Rig.Tick(1f / 60); input.RecenterPressed = false;
            for (int i = 0; i < 240; i++) f.Rig.Tick(1f / 60);
            Assert.AreEqual(0, f.Rig.YawDeg, .001f); Assert.AreEqual(55, Pitch(f), .001f);
        }
        [Test] public void FollowHeading_YawTracksShip()
        {
            using var f = new Fixture(); Start(f);
            f.Rig.SetTarget(float3.zero, quaternion.RotateY(math.radians(90)), float3.zero, 10, false);
            for (int i = 0; i < 240; i++) f.Rig.Tick(1f / 60);
            Assert.AreEqual(90, f.Rig.YawDeg, .001f);
            Assert.Less(f.Rig.Camera.transform.position.x, -80);
        }
        [Test] public void FrameRateIndependence_30_60_144()
        {
            var rates = new[] { 30, 60, 144 }; var positions = new float3[3];
            for (int r = 0; r < rates.Length; r++)
            {
                int fps = rates[r]; using var f = new Fixture(); Start(f, new float3(100, 0, 0));
                for (int frame = 1; frame <= fps * 2; frame++)
                {
                    f.Rig.SetTarget(new float3(frame * (100f / fps), 0, 0), quaternion.identity, new float3(100, 0, 0), 10, false);
                    f.Rig.Tick(1f / fps);
                }
                positions[r] = f.Rig.Camera.transform.position;
                TestContext.WriteLine("FPS " + fps + ": " + positions[r]);
            }
            Assert.LessOrEqual(math.distance(positions[0], positions[1]), .05f);
            Assert.LessOrEqual(math.distance(positions[0], positions[2]), .05f);
        }
        [Test] public void LookAhead_ClampedToFractionOfDistance()
        {
            using var f = new Fixture(); Start(f, new float3(1000, 20, 0));
            for (int i = 0; i < 600; i++)
            {
                f.Rig.Tick(1f / 60);
                Assert.LessOrEqual(math.length(f.Rig.LookAhead), f.Config.LookAheadMaxFraction * f.Rig.CurrentDistance + 1e-4f);
                Assert.AreEqual(0, f.Rig.LookAhead.y);
            }
            Assert.AreEqual(52.5f, math.length(f.Rig.LookAhead), .001f);
        }
    }
}
