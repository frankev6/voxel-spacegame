using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Systems;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>SPEC-31 streaming regressions; FND-011 owns the complete pause-menu/player-spawn flow.</summary>
    public sealed class RegressionTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() => EventHub.Reset();

        // Far from any authored body: these cases exercise entry/cache restoration, not voxel streaming.
        static readonly double3 Position = new(1234567890123.125, 700.5, -7500000.25);

        [Test] public void R9_GeneratedOncePerLoad()
        {
            using var f = new StarSystemLoadTests.Fixture();
            StarSystemId start = f.State.CurrentStarSystem, away = OtherAuthoredSystem(f);
            f.State.PlayerLocation = new PlayerLocation { StarSystem = start, PositionM = Position };
            Assert.AreEqual(1, f.Galaxy.ContentGenerationCount(start), "GetStartLocation already generated the start content.");
            f.Load(); Assert.IsTrue(f.Loader.IsArrivalReady);
            Assert.AreEqual(1, f.Galaxy.ContentGenerationCount(start));
            Assert.AreEqual(1, f.Galaxy.GenerationCount);
            for (int i = 0; i < 3; i++) { f.Tick(); f.Galaxy.GetContent(start); }
            Assert.AreEqual(1, f.Galaxy.ContentGenerationCount(start), "Ready ticks and lookups must reuse this load's content.");

            // SYS-013 supplies this real hook; no test-side unload/load substitute.
            Assert.IsTrue(f.Loader.BeginStarSystemChange(away, Position, 0, StarSystemEntryReason.Jump));
            CompleteLoad(f);
            Assert.AreEqual(away, f.Loader.Loaded);
            Assert.AreEqual(1, f.Galaxy.ContentGenerationCount(away));
            Assert.AreEqual(1, f.Galaxy.ContentGenerationCount(start));

            Assert.IsTrue(f.Loader.BeginStarSystemChange(start, Position, 0, StarSystemEntryReason.Jump));
            CompleteLoad(f);
            Assert.AreEqual(start, f.Loader.Loaded);
            for (int i = 0; i < 3; i++) { f.Tick(); f.Galaxy.GetContent(start); }
            Assert.AreEqual(2, f.Galaxy.ContentGenerationCount(start), "Returning generates once for the second load.");
            Assert.AreEqual(1, f.Galaxy.ContentGenerationCount(away));
            Assert.AreEqual(1, f.Galaxy.GenerationCount, "Changing star systems must retain the generated galaxy.");
        }

        [Test] public void R28_PauseLoad_KeepsPosition()
        {
            var docking = new DockingService();
            using var f = new StarSystemLoadTests.Fixture(docking: docking);
            StarSystemId savedSystem = OtherAuthoredSystem(f);
            f.State.CurrentStarSystem = savedSystem;
            f.State.PlayerLocation = new PlayerLocation { StarSystem = savedSystem, PositionM = Position, YawRad = 1.25f };
            var content = f.Galaxy.GetContent(savedSystem);
            var removed = content.Bodies[content.Bodies.Length - 1].Id;
            Assert.IsTrue(f.Galaxy.MarkBodyDestroyed(removed));
            f.Load(); Assert.IsTrue(f.Loader.IsArrivalReady);
            int bodyCount = BodyCount(f);
            Assert.Greater(bodyCount, 0);
            Assert.AreEqual(content.Bodies.Length - 1, bodyCount);
            var galaxySave = new GalaxySaveParticipant(f.State, f.Catalog);
            var locationSave = new PlayerLocationSaveParticipant(f.State, docking);
            byte[] galaxyBytes, locationBytes;
            using (var writer = new SaveWriter()) { galaxySave.Capture(writer); galaxyBytes = writer.ToArray(); }
            using (var writer = new SaveWriter()) { locationSave.Capture(writer); locationBytes = writer.ToArray(); }

            f.Unload(); Assert.AreEqual(0, BodyCount(f));
            // Replace both live pose and frame through their owners so stale state cannot satisfy the restore.
            f.State.CurrentStarSystem = f.Galaxy.Galaxy.StartStarSystem;
            f.State.PlayerLocation = default; f.State.HasPlayerLocation = false;
            f.Galaxy.RequestOriginReset(f.State.CurrentStarSystem, double3.zero);
            ApplyOrigin(f);
            galaxySave.Restore(new SaveReader(galaxyBytes), galaxySave.Version);
            locationSave.Restore(new SaveReader(locationBytes), locationSave.Version);
            f.Galaxy.OnSessionRestored();
            var entry = new SceneReadyContext(GameScene.World, SceneEntryReason.LoadGame, default);
            f.Loader.OnSceneReady(entry); CompleteLoad(f);

            Assert.IsTrue(f.Loader.IsSceneReadyComplete(entry));
            Assert.AreEqual(savedSystem, f.Loader.Loaded);
            Assert.AreEqual(savedSystem, f.State.CurrentStarSystem);
            Assert.IsTrue(f.State.HasPlayerLocation);
            CollectionAssert.Contains(f.State.GetOrCreateDelta(savedSystem).Destroyed, removed.LocalKey);
            Assert.AreEqual(savedSystem, f.State.PlayerLocation.StarSystem);
            Assert.LessOrEqual(math.distance(Position, f.State.PlayerLocation.PositionM), 0.01);
            Assert.AreEqual(1.25f, f.State.PlayerLocation.YawRad);
            using var origins = f.Em.CreateEntityQuery(typeof(FrameOrigin));
            var origin = origins.GetSingleton<FrameOrigin>();
            Assert.AreEqual(savedSystem, origin.StarSystem);
            Assert.LessOrEqual(math.distance(Position, origin.OriginInStarSystem), 0.01,
                "The loader must establish the saved player-spawn origin, not the campaign start location.");
            Assert.AreEqual(bodyCount, BodyCount(f));
            using var info = f.Em.CreateEntityQuery(typeof(CurrentStarSystemInfo));
            Assert.AreEqual(bodyCount, info.GetSingleton<CurrentStarSystemInfo>().BodyCount);
        }

        static StarSystemId OtherAuthoredSystem(StarSystemLoadTests.Fixture f)
        {
            foreach (var system in f.Galaxy.Galaxy.StarSystems)
                if (system.AuthoredIndex != 0 && !system.Id.Equals(f.Galaxy.Galaxy.StartStarSystem)) return system.Id;
            Assert.Fail("The production fixture requires another authored star system.");
            return default;
        }
        static void ApplyOrigin(StarSystemLoadTests.Fixture f)
        {
            f.World.GetExistingSystem<OriginRebaseSystem>().Update(f.World.Unmanaged);
            f.Em.CompleteAllTrackedJobs();
        }
        static void CompleteLoad(StarSystemLoadTests.Fixture f)
        {
            Assert.AreEqual(LoaderState.Loading, f.Loader.State);
            f.Tick(); Assert.AreEqual(LoaderState.Loading, f.Loader.State, "Entry waits for the actual origin owner.");
            ApplyOrigin(f); f.Tick();
            Assert.IsTrue(f.Loader.IsArrivalReady, "Two direct loader ticks and one origin update complete these remote arrivals.");
        }
        static int BodyCount(StarSystemLoadTests.Fixture f)
        {
            using var bodies = f.Em.CreateEntityQuery(typeof(StarSystemBody));
            return bodies.CalculateEntityCount();
        }
    }
}
