using System.Collections;
using Game.App.Flow;
using Game.Core;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Simulation.Commands;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>FND-016: sandbox entry, optional player and no streamed star-system ownership (SPEC-01 §5.4.4).</summary>
    public sealed class SandboxTests
    {
        [UnityTest]
        public IEnumerator Sandbox_SpawnsPlayer()
        {
            using var f = new PlayerShipTests.Fixture(false);
            yield return f.Run(f.Flow.TransitionAsync(GameScene.MainMenu, SceneEntryReason.Startup));
            foreach (bool spawnPlayer in new[] { true, false })
            {
                var entry = f.Flow.EnterSandboxAsync(new SandboxArgs(spawnPlayer, 123));
                Assert.AreEqual(FlowResult.Busy, f.Flow.EnterSandboxAsync(new SandboxArgs(true, 456)).GetAwaiter().GetResult());
                yield return f.Run(entry);
                Assert.AreEqual(GameScene.World, f.Flow.CurrentScene);
                Assert.IsTrue(f.Session.HasActiveSession && f.Session.IsCreativeSession && f.Session.IsSandbox);
                Assert.AreEqual(123ul, f.Session.CampaignSeed);
                using var active = f.EM.CreateEntityQuery(typeof(GameplayActive));
                Assert.AreEqual(1, active.CalculateEntityCount());
                using var players = f.EM.CreateEntityQuery(typeof(PlayerShip));
                Assert.AreEqual(spawnPlayer ? 1 : 0, players.CalculateEntityCount());
                if (spawnPlayer) Assert.AreEqual(float3.zero, f.EM.GetComponentData<LocalTransform>(f.Root()).Position);
                else Assert.IsFalse(f.Session.PlayerSpawner.TryGetRoot(out _));
                using (var entities = f.EM.GetAllEntities(Allocator.Temp))
                    foreach (var entity in entities)
                    {
                        using var types = f.EM.GetComponentTypes(entity);
                        foreach (var type in types) Assert.AreNotEqual("StarSystemMember", TypeManager.GetType(type.TypeIndex).Name);
                    }
            }
            yield return f.Run(f.Session.QuitToMainMenuAsync(false));
            yield return f.Run(f.Session.NewGameAsync(new NewGameArgs("Pilot", 789, false, ContentId.None)));
            Assert.IsFalse(f.Session.IsSandbox);
            Assert.IsTrue(f.Session.PlayerSpawner.TryGetRoot(out _), "A sandbox's no-player option must not persist into a new campaign");
            yield return f.Run(f.Session.QuitToMainMenuAsync(false));
        }
    }
}
