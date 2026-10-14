using System.Collections;
using Game.Core;
using Game.Core.Flow;
using Game.Tests.PlayMode.App;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Presentation
{
    /// <summary>SPEC-62 CAM-012: imported hull, production camera bridge and independent viewport projection.</summary>
    public sealed class FlightFramingTests : ProductionSessionFixture
    {
        IEnumerator StartShip(float scroll, Vector2 orbit, float expectedDistance)
        {
            yield return Run(Game.Session.NewGameAsync(new NewGameArgs("Harness", 42, false, ContentId.None)));
            yield return Until(() => Game.Input.CurrentContext == InputContext.Flight && Game.Camera.HasTarget, "Player camera ready");
            // SPEC-62 applies impact shake after framing; this test measures the unshaken framing contract.
            Game.Camera.ApplySettings(new Game.Presentation.Data.CameraSettings { MouseSensitivity = 1, ShakeScale = 0 });
            Assert.AreEqual("ship_fighter", Game.Session.Fleet.Flagship.SourceBlueprint.Value);
            Assert.AreEqual(1005, Game.Session.Fleet.Flagship.Structure.SolidCellCount);
            // The starter now spawns beside a station; framing is measured in open space, away from camera occlusion.
            var moved = Host.Commands.Execute("teleport x=1000 z=-1000 yaw=0", Host.Context);
            Assert.AreEqual(global::Game.App.DevRuntime.DevResultKind.Ok, moved.Kind, moved.Text);
            yield return null; yield return null;
            Host.Input.SetMouse(640, 360, "none", false, scroll, 0, 0);
            double zoomDeadline = Time.realtimeSinceStartupAsDouble + 10;
            while (math.abs(Game.Camera.CurrentDistance - expectedDistance) >= .01f && Time.realtimeSinceStartupAsDouble < zoomDeadline) yield return null;
            Assert.AreEqual(expectedDistance, Game.Camera.CurrentDistance, .01f, "Requested zoom settled");
            Host.Input.SetMouse(-1, -1, "middle", true, 0, orbit.x, orbit.y);
            yield return Until(() => math.abs(Game.Camera.YawDeg - orbit.x * .25f) < .01f, "Requested orbit settled");
            Host.Input.SetMouse(-1, -1, "middle", false, 0, 0, 0);
            yield return null;
        }
        float3 LocalPoint(int corner)
        {
            var grid = EM.GetComponentData<VoxelGrid>(Root);
            float3 min = (float3)grid.CellMin * grid.CellSize, max = ((float3)grid.CellMax + 1) * grid.CellSize;
            return new float3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
        }
        Vector3 Project(float3 local) => Game.Camera.Camera.WorldToViewportPoint(
            math.transform(EM.GetComponentData<LocalToWorld>(Root).Value, local));
        static bool Visible(Vector3 p) => p.z > 0 && p.x >= -.0003f && p.x <= 1.0003f && p.y >= -.0003f && p.y <= 1.0003f;
        void AssertHullVisible()
        {
            for (int corner = 0; corner < 8; corner++)
            {
                var point = Project(LocalPoint(corner));
                Assert.IsTrue(Visible(point), "Hull corner " + corner + " clipped at " + point.ToString("F4") + ", aspect=" + Game.Camera.Camera.aspect);
            }
        }
        void SetFlight(float heading)
        {
            var pose = EM.GetComponentData<LocalTransform>(Root); pose.Rotation = quaternion.RotateY(math.radians(heading));
            EM.SetComponentData(Root, pose);
            EM.SetComponentData(Root, new PhysicsVelocity { Linear = new float3(0, 0, 53) });
        }
        [UnityTest] public IEnumerator Fighter_FastFlight_KeepsFittingHullInViewport()
        {
            float distance = 150 * math.pow(.88f, 1200 * .008333f);
            yield return StartShip(1200, new Vector2(100, 20), distance);
            foreach (float aspect in new[] { 16f / 9, 4f / 3 })
            {
                Game.Camera.Camera.aspect = aspect; yield return null;
                AssertHullVisible();
                float elapsed = 0; int orbitState = 0;
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (elapsed < 2)
                {
                    SetFlight(elapsed * 60);
                    if (elapsed >= 1 && orbitState == 0)
                    { Host.Input.SetMouse(-1, -1, "middle", true, 0, 120, -40); orbitState = 1; }
                    else if (orbitState == 1)
                    { Host.Input.SetMouse(-1, -1, "middle", false, 0, 0, 0); orbitState = 2; }
                    yield return null;
                    elapsed += math.clamp(Time.unscaledDeltaTime, 0, .1f);
                    Assert.Less(Time.realtimeSinceStartupAsDouble, deadline);
                    AssertHullVisible();
                    Assert.AreEqual(distance, Game.Camera.CurrentDistance, .01f, "Framing must not change zoom");
                }
            }
            TestContext.WriteLine("Fighter: all eight solid-bounds corners framed at 53 m/s, 16:9 and 4:3, with changing heading and a 25-degree orbit offset.");
        }
        [UnityTest] public IEnumerator CloseInspection_KeepsCenterWithoutResettingInput()
        {
            yield return StartShip(12000, new Vector2(180, 80), 25);
            Game.Camera.Camera.aspect = 4f / 3;
            float elapsed = 0; bool sawClippedCorner = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (elapsed < 1)
            {
                SetFlight(0); yield return null;
                elapsed += math.clamp(Time.unscaledDeltaTime, 0, .1f);
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline);
                Assert.IsTrue(Visible(Project((LocalPoint(0) + LocalPoint(7)) * .5f)), "Hull center remains visible");
                Assert.AreEqual(25, Game.Camera.CurrentDistance, .01f);
                Assert.AreEqual(45, Game.Camera.YawDeg, .02f, "Orbit offset preserved");
                for (int corner = 0; corner < 8; corner++) sawClippedCorner |= !Visible(Project(LocalPoint(corner)));
            }
            Assert.IsTrue(sawClippedCorner, "Fixture must really exercise a hull too large for the requested viewport");
        }
    }
}
