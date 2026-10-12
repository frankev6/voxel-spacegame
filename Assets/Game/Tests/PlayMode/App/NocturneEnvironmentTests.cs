using System.Collections;
using Game.Core;
using Game.Core.Flow;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.App
{
    public sealed class NocturneEnvironmentTests : ProductionSessionFixture
    {
        [UnityTest] public IEnumerator NewGameRestart_PreservesSky()
        {
            var clear = Game.Camera.Camera.clearFlags;
            for (int restart = 0; restart < 3; restart++)
            {
                yield return Run(Game.Session.NewGameAsync(new NewGameArgs("Harness", 42, false, ContentId.None)));
                yield return Until(() => Object.FindAnyObjectByType<NocturneFlightEnvironment>()?.GetComponentInChildren<ParticleSystem>() != null,
                    "Environment initialized after direct restart");
                // Let deferred destruction settle before accepting a component that is still queued for removal.
                yield return null;
                var skybox = Game.Camera.Camera.GetComponent<Skybox>();
                Assert.IsNotNull(skybox, "Direct restart " + restart + " must retain the camera override");
                Assert.IsTrue(skybox.enabled); Assert.AreEqual("Game/NocturneSky", skybox.material.shader.name);
                Assert.AreEqual(1, Object.FindObjectsByType<NocturneFlightEnvironment>().Length);
                var environment = Object.FindAnyObjectByType<NocturneFlightEnvironment>();
                Assert.AreEqual(1, environment.GetComponentsInChildren<ParticleSystem>().Length);
                Assert.AreEqual(1400, environment.GetComponentInChildren<ParticleSystem>().particleCount);
            }
            yield return Run(Game.Session.QuitToMainMenuAsync(false)); yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<NocturneFlightEnvironment>());
            Assert.IsNull(Game.Camera.Camera.GetComponent<Skybox>());
            Assert.AreEqual(clear, Game.Camera.Camera.clearFlags);
        }

        [UnityTest] public IEnumerator WorldEnvironment_EntersMovesAndCleansUp()
        {
            var clear = Game.Camera.Camera.clearFlags;
            for (int entry = 0; entry < 2; entry++)
            {
                yield return Run(Game.Session.NewGameAsync(new NewGameArgs("Harness", 42, false, ContentId.None)));
                yield return Until(() => Object.FindAnyObjectByType<NocturneFlightEnvironment>()?.GetComponentInChildren<ParticleSystem>() != null, "World environment initialized");
                yield return null;
                var environment = Object.FindAnyObjectByType<NocturneFlightEnvironment>();
                var dust = environment.GetComponentInChildren<ParticleSystem>();
                Assert.IsNotNull(dust);
                Assert.AreEqual(1400, dust.particleCount);
                Assert.AreEqual(CameraClearFlags.Skybox, Game.Camera.Camera.clearFlags);
                var sky = Game.Camera.Camera.GetComponent<Skybox>().material;
                Assert.AreEqual("Game/NocturneSky", sky.shader.name);
                Assert.Greater(sky.GetFloat("_NebulaSpeed"), 0, "Nebula evolves procedurally");
                var before = new ParticleSystem.Particle[1400];
                var after = new ParticleSystem.Particle[1400];
                dust.GetParticles(before);
                var cameraBefore = Game.Camera.Camera.transform.position;
                var planet = environment.transform.Find("VESPER-9 placeholder sphere");
                Assert.IsNotNull(planet, "Planet is actual scene geometry, separate from the sky");
                Assert.IsNotNull(planet.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsNotNull(planet.Find("VESPER-9 atmosphere").GetComponent<MeshRenderer>());
                var planetPosition = planet.position;
                var planetViewport = Game.Camera.Camera.WorldToViewportPoint(planetPosition);
                var start = Position;
                Host.Input.SetKeys("W", 0, false);
                yield return Until(() => Unity.Mathematics.math.distance(start, Position) > 3, "Real flight advances through debris");
                Host.Input.SetKeys("W", 0, true);
                dust.GetParticles(after);
                Assert.AreEqual(planetPosition, planet.position, "Planet remains at its world position");
                Assert.Greater(Vector3.Distance(planetViewport, Game.Camera.Camera.WorldToViewportPoint(planetPosition)), .001f, "Camera translates relative to the finite planet");
                int stationary = 0;
                for (int i = 0; i < before.Length; i++)
                    if (Vector3.Distance(before[i].position, after[i].position) < .001f) stationary++;
                Assert.Greater(stationary, 1000, "Most particles retain world positions while the camera moves");
                Assert.Greater(Vector3.Distance(cameraBefore, Game.Camera.Camera.transform.position), .1f);
                yield return Run(Game.Session.QuitToMainMenuAsync(false));
                yield return null;
                Assert.IsTrue(environment == null && dust == null, "Scene owns and destroys presentation");
                Assert.AreEqual(clear, Game.Camera.Camera.clearFlags);
                Assert.IsNull(Game.Camera.Camera.GetComponent<Skybox>());
            }
        }
    }
}
