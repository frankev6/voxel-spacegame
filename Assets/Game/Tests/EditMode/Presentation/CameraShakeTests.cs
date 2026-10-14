using Game.Data.Definitions;
using Game.Presentation.Data;
using Game.Presentation.Services;
using Game.Presentation.Systems;
using Game.Presentation.Vfx;
using Game.Simulation.Commands;
using Game.Tests.Common;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Fixture = Game.Tests.EditMode.Presentation.CameraRigServiceTests.Fixture;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>SPEC-62 CAM-008: saturating trauma, per-frame peak hold (QA-001), accessibility settings and the bounded FX frame reader.</summary>
    public sealed class CameraShakeTests : GameTestBase
    {
        static void Start(Fixture f)
        {
            f.Rig.SetMode(CameraMode.Flight, true);
            f.Rig.SetTarget(new float3(12, 0, 20), quaternion.identity, float3.zero, 10, true);
            f.Rig.Tick(0);
        }

        [Test] public void Impulse_AtZero_TraumaOne()
        {
            using var f = new Fixture(); Start(f);
            f.Rig.AddImpulse(f.Rig.Focus, 1, 300);
            Assert.AreEqual(1, f.Rig.Trauma);
            f.Rig.AddImpulse(f.Rig.Focus, 1, 300);
            Assert.AreEqual(1, f.Rig.Trauma, "Accumulated trauma saturates at one.");

            var config = AssetDatabase.LoadAssetAtPath<CameraConfig>("Assets/Game/Presentation/Data/CameraConfig.asset");
            Assert.NotNull(config);
            Assert.AreEqual(5, config.ShakeRules.Length);
            var expected = new[]
            {
                new ShakeRule { Kind = FxKind.ExplosionSmall, Strength = .35f, RadiusMeters = 150 },
                new ShakeRule { Kind = FxKind.ExplosionLarge, Strength = .6f, RadiusMeters = 400 },
                new ShakeRule { Kind = FxKind.RamImpact, Strength = .2f, RadiusMeters = 120 },
                new ShakeRule { Kind = FxKind.HitKinetic, Strength = .04f, RadiusMeters = 60 },
                new ShakeRule { Kind = FxKind.HitEnergy, Strength = .04f, RadiusMeters = 60 }
            };
            CollectionAssert.AreEquivalent(expected, config.ShakeRules);
        }

        [Test] public void Trauma_Decays()
        {
            using var f = new Fixture(); Start(f);
            f.Rig.AddImpulse(f.Rig.Focus, 1, 300);
            float elapsed = 1f / 60;
            f.Rig.Tick(elapsed);
            Assert.AreEqual(1 - f.Config.TraumaDecayPerSecond * elapsed, f.Rig.Trauma, 1e-6f);
            float end = 1 / f.Config.TraumaDecayPerSecond;
            while (elapsed < end)
            {
                float dt = math.min(1f / 60, end - elapsed);
                f.Rig.Tick(dt); elapsed += dt;
            }
            Assert.AreEqual(0, f.Rig.Trauma, 1e-6f);
            f.Rig.Tick(1f / 60);
            Assert.AreEqual(0, f.Rig.Trauma);
        }

        [Test] public void Impulse_BeyondRadius_Ignored()
        {
            using var f = new Fixture(); Start(f);
            f.Rig.AddImpulse(f.Rig.Focus + new float3(301, 0, 0), 1, 300);
            Assert.AreEqual(0, f.Rig.Trauma);
            f.Rig.AddImpulse(f.Rig.Focus + new float3(300, 0, 0), 1, 300);
            Assert.AreEqual(0, f.Rig.Trauma);
            f.Rig.AddImpulse(f.Rig.Focus + new float3(150, 0, 0), 1, 300);
            Assert.AreEqual(.5f, f.Rig.Trauma, 1e-6f, "Linear distance falloff.");
        }

        [Test] public void ShakeScaleZero_NoOffset()
        {
            using var f = new Fixture(); Start(f);
            var position = f.Rig.Camera.transform.position; var rotation = f.Rig.Camera.transform.rotation;
            f.Rig.ApplySettings(new CameraSettings { MouseSensitivity = 1, ShakeScale = 0 });
            f.Rig.AddImpulse(f.Rig.Focus, 1, 300);
            for (int i = 0; i < 10; i++)
            {
                f.Rig.Tick(1f / 60);
                Assert.AreEqual(position, f.Rig.Camera.transform.position);
                Assert.AreEqual(rotation, f.Rig.Camera.transform.rotation);
            }
            Assert.Greater(f.Rig.Trauma, 0, "Setting suppresses the output, even with active trauma.");
        }

        [Test] public void ReduceMotion_NoOffset()
        {
            using var f = new Fixture(); Start(f);
            var position = f.Rig.Camera.transform.position; var rotation = f.Rig.Camera.transform.rotation;
            f.Rig.AddImpulse(f.Rig.Focus, 1, 300); f.Rig.Tick(1f / 60);
            Assert.Greater(Vector3.Distance(position, f.Rig.Camera.transform.position), 1e-5f);
            f.Rig.ApplySettings(new CameraSettings { MouseSensitivity = 1, ShakeScale = 1, ReduceMotion = true });
            for (int i = 0; i < 10; i++)
            {
                f.Rig.Tick(1f / 60);
                Assert.AreEqual(position, f.Rig.Camera.transform.position);
                Assert.AreEqual(rotation, f.Rig.Camera.transform.rotation);
            }
            Assert.Greater(f.Rig.Trauma, 0);
        }

        [Test] public void Shake_DoesNotMoveFocus()
        {
            using var f = new Fixture(); Start(f);
            var focus = f.Rig.Focus; var state = f.Rig.FocusState;
            var unshakenPosition = f.Rig.DesiredFlightPosition(); var rotation = f.Rig.Camera.transform.rotation;
            f.Rig.AddImpulse(focus, 1, 300);
            f.Rig.Tick(1f / 60);
            Assert.Greater(math.distance(unshakenPosition, f.Rig.Camera.transform.position), 1e-5f);
            Assert.Greater(Quaternion.Angle(rotation, f.Rig.Camera.transform.rotation), 1e-4f);
            for (int i = 0; i < 60; i++)
            {
                Assert.AreEqual(focus, f.Rig.Focus);
                Assert.AreEqual(state, f.Rig.FocusState);
                Assert.AreEqual(unshakenPosition, f.Rig.DesiredFlightPosition());
                f.Rig.Tick(1f / 60);
            }
            Assert.AreEqual(unshakenPosition, (float3)f.Rig.Camera.transform.position);
            Assert.AreEqual(rotation, f.Rig.Camera.transform.rotation);
        }

        [Test] public void Over256Events_Capped()
        {
            using var f = new Fixture(); Start(f);
            using var w = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem), typeof(FxUploadSystem), typeof(CameraTargetSyncSystem));
            var upload = w.World.GetExistingSystemManaged<FxUploadSystem>();
            var reader = new FxFrameReader(upload);
            var sync = w.World.GetExistingSystemManaged<CameraTargetSyncSystem>();
            sync.Bind(f.Rig, reader);
            var ship = w.EntityManager.CreateEntity(typeof(PlayerShip), typeof(LocalToWorld));
            w.EntityManager.SetComponentData(ship, new LocalToWorld { Value = float4x4.Translate(f.Rig.Focus) });
            f.Config.ShakeRules = new[] { new ShakeRule { Kind = FxKind.ExplosionSmall, Strength = .1f, RadiusMeters = 300 } };
            var queue = w.GetSingleton<FxEventQueue>().Queue;
            for (int i = 0; i < 256; i++) queue.Enqueue(new FxEvent { Kind = FxKind.None });
            queue.Enqueue(new FxEvent { Kind = FxKind.ExplosionSmall, Pos = f.Rig.Focus, Scale = 1000 });
            w.StepFrame(0);
            Assert.AreEqual(257, reader.Current.Length, "Reader borrows the completed upload's full drain.");
            Assert.AreEqual(0, f.Rig.Trauma, "The limit counts examined events, including unmatched kinds.");
            for (int i = 0; i < 256; i++) queue.Enqueue(new FxEvent { Kind = FxKind.ExplosionSmall, Pos = f.Rig.Focus, Scale = 2 });
            queue.Enqueue(new FxEvent { Kind = FxKind.ExplosionSmall, Pos = f.Rig.Focus, Scale = 1000 });
            w.StepFrame(0);
            Assert.AreEqual(257, reader.Current.Length);
            float expected = .2f - f.Config.TraumaDecayPerSecond * math.clamp(Time.unscaledDeltaTime, 0, .1f);
            Assert.AreEqual(expected, f.Rig.Trauma, 1e-5f, "Peak hold: the strongest of the first 256 events applies once; the final event is ignored.");
            w.StepFrame(0);
            Assert.AreEqual(0, reader.Current.Length, "Empty next drain does not replay previous-frame events.");
        }
    }
}
