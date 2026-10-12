using System;
using Game.Presentation.Data;
using Game.Presentation.Services;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>CAM-004: rig lifetime, menu pose and mode blending.</summary>
    public sealed class CameraRigServiceTests
    {
        internal sealed class Fixture : IDisposable
        {
            internal readonly GameObject Parent = new GameObject("Camera test parent");
            internal readonly GameObject Prefab = new GameObject("Camera test prefab", typeof(Camera));
            internal readonly CameraConfig Config = ScriptableObject.CreateInstance<CameraConfig>();
            internal CameraRigService Rig;
            internal Fixture(bool construct = true, ICameraInput input = null)
            {
                Config.CameraPrefab = Prefab;
                if (construct) Rig = new CameraRigService(Config, Parent.transform, input);
            }
            public void Dispose()
            {
                Rig?.Dispose(); Object.DestroyImmediate(Parent); Object.DestroyImmediate(Prefab); Object.DestroyImmediate(Config);
            }
        }
        [Test] public void Construct_CreatesOneCamera()
        {
            using var f = new Fixture();
            Assert.AreEqual(1, f.Parent.GetComponentsInChildren<Camera>(true).Length);
            Assert.AreEqual(0, f.Parent.GetComponentsInChildren<AudioListener>(true).Length);
            Assert.AreEqual(f.Config.VerticalFovDeg, f.Rig.Camera.fieldOfView);
            Assert.AreEqual(f.Config.NearClip, f.Rig.Camera.nearClipPlane);
            Assert.AreEqual(f.Config.FarClip, f.Rig.Camera.farClipPlane);
            Assert.IsTrue(f.Rig.ListenerPivot.IsChildOf(f.Parent.transform));
        }
        [Test] public void Dispose_DestroysCamera()
        {
            using var f = new Fixture(); var camera = f.Rig.Camera; var pivot = f.Rig.ListenerPivot;
            f.Rig.Dispose(); Assert.IsTrue(camera == null); Assert.IsTrue(pivot == null);
            Assert.AreEqual(0, f.Parent.transform.childCount);
        }
        [Test] public void MinPitchBelow20_Throws()
        {
            using var f = new Fixture(false); f.Config.MinPitchDeg = 19;
            Assert.Throws<InvalidOperationException>(() => new CameraRigService(f.Config, f.Parent.transform, null));
            Assert.AreEqual(0, f.Parent.transform.childCount);
        }
        [Test] public void SetMode_Blend_ReachesTargetAfterBlendSeconds()
        {
            using var f = new Fixture(); f.Config.MenuDriftDegPerSecond = 0;
            var start = f.Rig.Camera.transform.position;
            f.Rig.SetMode(CameraMode.Hold, true); f.Config.MenuPosition += new Vector3(30, 0, 0);
            f.Rig.SetMode(CameraMode.Menu, false); f.Rig.Tick(.1f);
            Assert.Greater(Vector3.Distance(start, f.Rig.Camera.transform.position), 0);
            Assert.Greater(Vector3.Distance(f.Config.MenuPosition, f.Rig.Camera.transform.position), 1);
            for (int i = 1; i < 5; i++) f.Rig.Tick(.1f);
            Assert.Less(Vector3.Distance(f.Config.MenuPosition, f.Rig.Camera.transform.position), 1e-4f);
            var held = f.Rig.Camera.transform.position; f.Rig.SetMode(CameraMode.Hold, true); f.Rig.Tick(.1f);
            Assert.AreEqual(held, f.Rig.Camera.transform.position);
        }
        [Test] public void SetMode_ReduceMotion_Cuts()
        {
            using var f = new Fixture();
            f.Rig.ApplySettings(new CameraSettings { MouseSensitivity = 1, ShakeScale = 1, ReduceMotion = true });
            f.Config.MenuPosition += new Vector3(30, 0, 0); f.Rig.SetMode(CameraMode.Menu, false); f.Rig.Tick(.1f);
            Assert.Less(Vector3.Distance(f.Config.MenuPosition, f.Rig.Camera.transform.position), 1e-4f);
        }
        [Test] public void Menu_Drift_TwoDegreesPerSecond()
        {
            using var f = new Fixture(); for (int i = 0; i < 60; i++) f.Rig.Tick(1f / 60);
            Assert.AreEqual(2, f.Rig.YawDeg, 1e-5f);
            var expected = math.rotate(quaternion.RotateY(math.radians(2)), (float3)f.Config.MenuPosition);
            Assert.Less(math.distance(expected, f.Rig.Camera.transform.position), 1e-4f);
            f.Rig.ApplySettings(new CameraSettings { ReduceMotion = true }); var position = f.Rig.Camera.transform.position;
            f.Rig.Tick(.1f); Assert.AreEqual(position, f.Rig.Camera.transform.position);
        }
    }
}
