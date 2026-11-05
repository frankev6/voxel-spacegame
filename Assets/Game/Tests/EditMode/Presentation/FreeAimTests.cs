using Game.Core;
using Game.Presentation.Data;
using Game.Presentation.Services;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Fixture = Game.Tests.EditMode.Presentation.CameraRigServiceTests.Fixture;
using FrameInput = Game.Tests.EditMode.Presentation.FlightCameraModeTests.FrameInput;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>CAM-013 (ADR-0123 decision 6): the free-aim chase camera of experimental 3D flight.</summary>
    public sealed class FreeAimTests
    {
        string previous;
        [SetUp] public void SetUp() => previous = FlightCameraPreset.Current.Name;
        [TearDown] public void TearDown() => FlightCameraPreset.TrySet(previous);   // the preset is process-wide (feel prototype static)

        static float LookPitchDeg(Fixture f) => math.degrees(math.asin(-f.Rig.Camera.transform.forward.y));   // + = looking down

        [Test] public void MouseDelta_TurnsAimWithinPitchLimit()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input);
            Assert.IsTrue(FlightCameraPreset.TrySet(FreeAim.PresetName));   // after the fixture, which resets the preset
            var preset = FlightCameraPreset.Current;
            Assert.IsTrue(preset.MouseLook); Assert.AreEqual(FlightCameraYaw.Free, preset.Yaw); Assert.AreEqual(.5f, preset.ReticleHeight);
            f.Rig.SetMode(CameraMode.Flight, true); f.Rig.SetTarget(float3.zero, quaternion.identity, float3.zero, 10, true); f.Rig.Tick(0);

            float yaw = f.Rig.YawDeg;
            input.OrbitDelta = new Vector2(200, 0); f.Rig.Tick(1f / 60); input.OrbitDelta = Vector2.zero;
            for (int i = 0; i < 120; i++) f.Rig.Tick(1f / 60);
            Assert.Greater(math.abs(f.Rig.YawDeg - yaw), 10, "Sideways mouse motion turns the view.");

            for (int i = 0; i < 240; i++) f.Rig.Tick(1f / 60);   // the mode blend from the menu pose is over
            var rng = RngStreams.For(62, RngPurpose.Test, 13);
            bool lookedUp = false;
            for (int frame = 0; frame < 1000; frame++)
            {
                input.OrbitDelta = rng.NextFloat2(new float2(-3000), new float2(3000));
                f.Rig.Tick(1f / 60);
                Assert.AreEqual(FreeAim.PresetName, FlightCameraPreset.Current.Name);
                float pitch = LookPitchDeg(f);
                Assert.That(pitch, Is.InRange(preset.MinPitchDeg - 1e-3f, preset.MaxPitchDeg + 1e-3f), "Frame " + frame);
                lookedUp |= pitch < -10;
            }
            Assert.IsTrue(lookedUp, "Free aim can look up from below the ship, unlike the planar presets.");
        }

        [Test] public void ChasePose_LooksAlongAim()
        {
            var input = new FrameInput(); using var f = new Fixture(input: input);
            Assert.IsTrue(FlightCameraPreset.TrySet(FreeAim.PresetName));
            var ship = new float3(10, 40, -5);
            f.Rig.SetMode(CameraMode.Flight, true); f.Rig.SetTarget(ship, quaternion.identity, float3.zero, 10, true); f.Rig.Tick(0);
            input.OrbitDelta = new Vector2(90, -120); f.Rig.Tick(1f / 60); input.OrbitDelta = Vector2.zero;
            for (int i = 0; i < 240; i++) f.Rig.Tick(1f / 60);
            var camera = f.Rig.Camera.transform;
            float3 forward = camera.forward, position = camera.position;
            // The reticle (screen centre) looks over the ship: the centre ray passes through the focus, so its far point is where the
            // nose is sent, straight ahead of the view.
            float3 toFocus = math.normalize(f.Rig.Focus - position);
            Assert.Greater(math.dot(forward, toFocus), .999f, "The camera sits behind the aim and looks along it.");
            float3 aim = FreeAim.FarPoint(position, forward);
            Assert.Greater(math.dot(math.normalize(aim - ship), forward), .99f, "Seen from the ship, the aim point is where the view looks.");
        }

        [Test] public void AimWorld_HitOrFarPoint()
        {
            Assert.AreEqual(new float3(0, 0, FreeAim.FarAimDistance), FreeAim.FarPoint(float3.zero, new float3(0, 0, 3)));
            // Switching in remembers the player's preset and gives it back when free aim ends.
            string saved = null;
            Assert.AreEqual(FreeAim.PresetName, FreeAim.Switch(true, "orbit", ref saved)); Assert.AreEqual("orbit", saved);
            Assert.IsNull(FreeAim.Switch(true, FreeAim.PresetName, ref saved), "Already in free aim: no switch.");
            Assert.AreEqual("orbit", FreeAim.Switch(false, FreeAim.PresetName, ref saved)); Assert.IsNull(saved);
            Assert.IsNull(FreeAim.Switch(false, "chase", ref saved), "Free aim off and not in its preset: leave the camera alone.");
            Assert.AreEqual("chase", FreeAim.Switch(false, FreeAim.PresetName, ref saved), "Nothing saved: back to the default chase camera.");
        }
    }
}
