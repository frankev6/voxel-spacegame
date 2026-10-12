using System;
using System.Collections;
using System.IO;
using Game.App.Boot;
using Game.App.DevRuntime;
using Game.App.Testing;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Simulation.Commands;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.App
{
    /// <summary>FND-017: the production boot, imported Fighter, input, physics, camera and save path in one running World.</summary>
    public sealed class PlayableShipTests : ProductionSessionFixture
    {
        IEnumerator StartShip()
        {
            yield return Run(Game.Session.NewGameAsync(new NewGameArgs("Harness", 42, false, ContentId.None)));
            yield return Until(() => Game.Input.CurrentContext == InputContext.Flight && Game.Camera.HasTarget, "Flight context and camera target");
            Assert.AreEqual("ship_fighter", Game.Session.Fleet.Flagship.SourceBlueprint.Value);
            Assert.AreEqual(1005, Game.Session.Fleet.Flagship.Structure.SolidCellCount);
            Assert.IsTrue(EM.HasComponent<PhysicsCollider>(Root));
            Assert.AreEqual(global::Game.Presentation.Data.CameraMode.Flight, Game.Camera.Mode);
        }
        [UnityTest] public IEnumerator Controls_MoveImportedShip()
        {
            yield return StartShip(); var start = Position;
            Host.Input.SetKeys("W", 0, false);
            yield return Until(() => math.distance(start, Position) > 2 && Speed > 1, "Real W input must move the imported ship");
            Assert.AreEqual(1, Command.Thrust.y); Host.Input.SetKeys("W", 0, true);
            var rotation = EM.GetComponentData<LocalTransform>(Root).Rotation;
            Host.Input.SetKeys("D", 0, false);
            yield return Until(() => math.abs(math.dot(rotation.value, EM.GetComponentData<LocalTransform>(Root).Rotation.value)) < .98f, "Real D input turns");
            Host.Input.SetKeys("D", 0, true);
            Host.Input.SetKeys("E", 0, false); yield return Until(() => Command.Thrust.x == 1, "Strafe action sampled");
            Host.Input.SetKeys("E", 0, true);
            float speed = Speed; Host.Input.SetKeys("X", 0, false);
            yield return Until(() => Speed < speed * .8f, "Real X braking reduces speed"); Host.Input.SetKeys("X", 0, true);
            float distance = Game.Camera.CurrentDistance;
            Host.Input.SetMouse(640, 360, "none", false, 120, 0, 0);
            yield return Until(() => Game.Camera.CurrentDistance < distance - 1, "Mouse wheel zooms flight camera");
            var cameraRotation = Game.Camera.Camera.transform.rotation;
            Host.Input.SetMouse(-1, -1, "middle", true, 0, 80, 0);
            yield return Until(() => Quaternion.Angle(cameraRotation, Game.Camera.Camera.transform.rotation) > 5, "Middle mouse orbits camera");
            Host.Input.SetMouse(-1, -1, "middle", false, 0, 0, 0);
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
        }
        [UnityTest] public IEnumerator SceneAndPause_NeutralizeFlight()
        {
            yield return StartShip(); Host.Input.SetKeys("W", 0, false);
            yield return Until(() => Command.Thrust.y == 1, "Held W");
            using (Game.Pause.Acquire("Checkpoint test"))
            {
                yield return Until(() => Game.Input.CurrentContext == InputContext.None && math.all(Command.Thrust == 0), "Pause neutralizes flight");
                var pausedAt = Position; yield return new WaitForSecondsRealtime(.1f);
                Assert.Less(math.distance(pausedAt, Position), .0001f);
            }
            yield return Until(() => Game.Input.CurrentContext == InputContext.Flight && Command.Thrust.y == 1, "Held W resumes");
            var quit = Game.Session.QuitToMainMenuAsync(false);
            yield return Until(() => math.all(Command.Thrust == 0), "Loading neutralizes flight");
            yield return Run(quit);
            yield return Until(() => Game.Input.CurrentContext == InputContext.Menu, "Menu context");
            Assert.AreEqual(float2.zero, Command.Thrust); Host.Input.SetKeys("W", 0, true);
        }
        [UnityTest] public IEnumerator SaveLoad_RestoresImportedShip()
        {
            yield return StartShip(); ulong id = Game.Session.Fleet.Flagship.PersistentId;
            var names = VoxsBlockNames.FromCatalog(Game.Content.Catalog);
            ulong hash = Game.Session.Fleet.Flagship.Structure.ComputeContentHash(names);
            Host.Input.SetKeys("W", 0, false); yield return Until(() => math.length(Position) > 2, "Flight before save");
            Host.Input.SetKeys("W", 0, true);
            yield return Run(Game.Session.SaveGameAsync("manual_017", SaveReason.Manual));
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
            yield return Run(Game.Session.LoadGameAsync("manual_017"));
            yield return Until(() => Game.Input.CurrentContext == InputContext.Flight && Game.Camera.HasTarget, "Loaded camera and input");
            Assert.AreEqual(id, Game.Session.Fleet.Flagship.PersistentId);
            Assert.AreEqual("ship_fighter", Game.Session.Fleet.Flagship.SourceBlueprint.Value);
            Assert.AreEqual(hash, Game.Session.Fleet.Flagship.Structure.ComputeContentHash(names), "All voxel bytes survive");
            Assert.IsTrue(EM.HasComponent<PhysicsCollider>(Root));
            // FLET v1 restores ship data; canonical world-location persistence has not landed (ADR-0044).
            Assert.Less(math.length(Position), .001f); Assert.Less(Speed, .001f);
            Host.Input.SetKeys("W", 0, false); yield return Until(() => math.length(Position) > 1, "Loaded ship still flies");
            Host.Input.SetKeys("W", 0, true); yield return Run(Game.Session.QuitToMainMenuAsync(false));
        }
    }
}
