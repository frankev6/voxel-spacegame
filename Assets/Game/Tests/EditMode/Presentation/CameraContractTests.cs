using Game.Presentation.Components;
using Game.Presentation.Data;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>CAM-001 contract layout and serialized tuning defaults.</summary>
    public sealed class CameraContractTests
    {
        [Test] public void MainCameraState_IsUnmanaged() => Assert.IsTrue(UnsafeUtility.IsUnmanaged<MainCameraState>());

        [Test] public void CameraConfig_Defaults_MatchSpec()
        {
            var c = ScriptableObject.CreateInstance<CameraConfig>();
            try
            {
                Assert.IsNull(c.CameraPrefab);
                Assert.AreEqual(40, c.VerticalFovDeg); Assert.AreEqual(.3f, c.NearClip); Assert.AreEqual(50000, c.FarClip);
                Assert.AreEqual(55, c.DefaultPitchDeg); Assert.AreEqual(35, c.MinPitchDeg); Assert.AreEqual(80, c.MaxPitchDeg);
                Assert.AreEqual(150, c.DefaultDistance); Assert.AreEqual(25, c.MinDistance); Assert.AreEqual(400, c.MaxDistance);
                Assert.AreEqual(.88f, c.ZoomStepFactor); Assert.AreEqual(.25f, c.OrbitDegPerPixel);
                Assert.AreEqual(.06f, c.FocusHalfLife); Assert.AreEqual(.12f, c.YawHalfLife);
                Assert.AreEqual(.08f, c.PitchHalfLife); Assert.AreEqual(.12f, c.ZoomHalfLife);
                Assert.AreEqual(.4f, c.LookAheadSeconds); Assert.AreEqual(.35f, c.LookAheadMaxFraction);
                Assert.AreEqual(.35f, c.LookAheadHalfLife); Assert.AreEqual(250, c.TeleportCutDistance);
                Assert.AreEqual(1, c.OcclusionMargin); Assert.AreEqual(5, c.OcclusionMinDistance); Assert.AreEqual(.3f, c.OcclusionReleaseHalfLife);
                Assert.AreEqual(-89, c.EditorMinPitchDeg); Assert.AreEqual(89, c.EditorMaxPitchDeg);
                Assert.AreEqual(2, c.EditorMinDistance); Assert.AreEqual(2.5f, c.EditorDistanceRadiusFactor); Assert.AreEqual(5, c.EditorDistancePad);
                Assert.AreEqual(6, c.EditorMaxDistanceRadiusFactor); Assert.AreEqual(100, c.EditorMaxDistancePad);
                Assert.AreEqual(.0015f, c.EditorPanPerPixelPerMeter); Assert.AreEqual(.05f, c.EditorHalfLife);
                Assert.AreEqual(1.2f, c.EditorFocusPadding); Assert.AreEqual(50, c.EditorPivotBoundsPad);
                Assert.AreEqual(.5f, c.BlendSeconds); Assert.AreEqual(new Vector3(0, 60, -120), c.MenuPosition);
                Assert.AreEqual(Vector3.zero, c.MenuLookAt); Assert.AreEqual(2, c.MenuDriftDegPerSecond);
                Assert.AreEqual(1.4f, c.TraumaDecayPerSecond); Assert.AreEqual(22, c.ShakeFrequencyHz);
                Assert.AreEqual(.01f, c.ShakeMaxOffsetFraction); Assert.AreEqual(2, c.ShakeMaxAngleDeg);
                Assert.IsNotNull(c.ShakeRules); Assert.AreEqual(0, c.ShakeRules.Length);
            }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
