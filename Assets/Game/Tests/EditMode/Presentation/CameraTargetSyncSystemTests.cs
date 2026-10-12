using Game.Presentation.Components;
using Game.Presentation.Data;
using Game.Presentation.Systems;
using Game.Simulation.Commands;
using Game.Tests.Common;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Fixture = Game.Tests.EditMode.Presentation.CameraRigServiceTests.Fixture;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>CAM-004: view singleton and target bridge outside gameplay.</summary>
    public sealed class CameraTargetSyncSystemTests : GameTestBase
    {
        [Test] public void Unbound_NoWrite()
        {
            using var w = EcsTestWorld.Create(typeof(CameraTargetSyncSystem));
            var before = w.GetSingleton<MainCameraState>(); w.StepFrame(0);
            Assert.AreEqual(before, w.GetSingleton<MainCameraState>());
        }
        [Test] public void Bound_WritesMainCameraStateEveryFrame()
        {
            using var f = new Fixture(); using var w = EcsTestWorld.Create(typeof(CameraTargetSyncSystem));
            w.World.GetExistingSystemManaged<CameraTargetSyncSystem>().Bind(f.Rig, null);
            f.Config.MenuDriftDegPerSecond = 0;
            for (int frame = 0; frame < 3; frame++)
            {
                f.Config.MenuPosition += new Vector3(1, 0, 0); w.StepFrame(0);
                var state = w.GetSingleton<MainCameraState>();
                Assert.AreEqual((float3)f.Rig.Camera.transform.position, state.Position);
                Assert.AreEqual((quaternion)f.Rig.Camera.transform.rotation, state.Rotation);
                Assert.AreEqual(f.Config.VerticalFovDeg, state.VerticalFovDeg);
                Assert.AreEqual((uint)Time.frameCount, state.FrameIndex);
                Assert.AreEqual((float4x4)(f.Rig.Camera.projectionMatrix * f.Rig.Camera.worldToCameraMatrix), state.ViewProjection);
                Assert.AreEqual(CameraMode.Menu, state.Mode);
            }
        }
        [Test] public void NoPlayerShip_ClearsTarget()
        {
            using var f = new Fixture(); using var w = EcsTestWorld.Create(typeof(CameraTargetSyncSystem));
            var em = w.EntityManager; var viewer = em.CreateSingleton<VoxelViewer>();
            var ship = em.CreateEntity(typeof(PlayerShip), typeof(LocalToWorld));
            var pos = new float3(50, 0, 20); em.SetComponentData(ship, new LocalToWorld { Value = float4x4.TRS(pos, quaternion.identity, new float3(1)) });
            f.Rig.SetMode(CameraMode.Flight, true);
            w.World.GetExistingSystemManaged<CameraTargetSyncSystem>().Bind(f.Rig, null); w.StepFrame(0);
            Assert.IsTrue(f.Rig.HasTarget); Assert.AreEqual(pos, f.Rig.TargetPosition);
            Assert.Less(math.distance(pos, f.Rig.ListenerPivot.position), 1e-4f, "Parent-space transform conversion");
            Assert.AreEqual(1, em.GetComponentData<VoxelViewer>(viewer).Valid);
            var held = f.Rig.Camera.transform.position;
            em.DestroyEntity(ship); w.StepFrame(0);
            Assert.IsFalse(f.Rig.HasTarget); Assert.AreEqual(held, f.Rig.Camera.transform.position);
            Assert.AreEqual(0, em.GetComponentData<VoxelViewer>(viewer).Valid);
        }
    }
}
