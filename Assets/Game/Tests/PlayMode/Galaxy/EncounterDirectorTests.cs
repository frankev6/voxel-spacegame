using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Factions;
using Game.Tests.Common;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>ENC-007: actual boot, loader, faction and spawn owners; no simulated director facade.</summary>
    public sealed class EncounterDirectorTests : GameFlowFixture
    {
        EncounterDirectorService Director => Service<EncounterDirectorService>();
        EncounterDefinition Raiders => Service<ContentService>().Catalog.Get<EncounterDefinition>(new ContentId("encounter_raiders"));
        void Tick(double time) => Director.Tick(new GalaxyFrameContext(Em, time, 0, 0, 0));
        [UnityTest] public IEnumerator Spawn_TagsAndAllegiance()
        {
            yield return NewGame(42); Director.Enabled = false;
            Assert.AreEqual(1, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
            using var query = Em.CreateEntityQuery(typeof(EncounterTag));
            using var roots = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(Director.AliveShipCount, roots.Length); Assert.Greater(roots.Length, 0);
            using var originQuery = Em.CreateEntityQuery(typeof(FrameOrigin));
            var origin = originQuery.GetSingleton<FrameOrigin>().OriginInStarSystem;
            var player = origin + (double3)Em.GetComponentData<Unity.Transforms.LocalTransform>(PlayerRoot).Position;
            uint instance = Em.GetComponentData<EncounterTag>(roots[0]).Instance;
            foreach (var root in roots)
            {
                var tag = Em.GetComponentData<EncounterTag>(root);
                Assert.AreEqual(instance, tag.Instance); Assert.AreEqual(Raiders.Id.Hash, tag.Encounter);
                Assert.AreEqual(Service<FactionService>().Indices.IndexOf(Raiders.Faction), Em.GetComponentData<Allegiance>(root).Faction);
                Assert.GreaterOrEqual(math.length((origin + (double3)Em.GetComponentData<GridSpawnRequest>(root).Pose.pos - player).xz), 800);
            }
            Assert.IsTrue(Director.TryGetInstance(instance, out var info)); Assert.AreEqual(roots.Length, info.AliveMembers);
        }
        [UnityTest] public IEnumerator Ambush_HeadsForPlayer()
        {
            yield return NewGame(42); Director.Enabled = false;
            Assert.AreEqual(1, Director.StartCombat(Raiders, EncounterAnchor.Player()));
            using var query = Em.CreateEntityQuery(typeof(EncounterTag), typeof(AiBrain));
            using var roots = query.ToEntityArray(Allocator.Temp);
            var player = Em.GetComponentData<Unity.Transforms.LocalTransform>(PlayerRoot).Position;
            Assert.Greater(roots.Length, 0);
            foreach (var root in roots)
            {
                var brain = Em.GetComponentData<AiBrain>(root);
                Assert.AreEqual(player, brain.Anchor); Assert.AreEqual(player, brain.GoalPosition);
                Assert.AreEqual(1, brain.Initialized);
            }
            // Sweep also handles not-yet-converted spawn requests without mistaking them for defeated members.
            Tick(1); Assert.AreEqual(roots.Length, Director.AliveShipCount);
        }
        [UnityTest] public IEnumerator Caps()
        {
            yield return NewGame(42); Director.Enabled = false;
            for (int i = 0; i < 5; i++) Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 10, EncounterSource.Director);
            Assert.LessOrEqual(Director.ActiveInstanceCount, 16); Assert.LessOrEqual(Director.AliveShipCount, 40);
            Assert.Greater(Director.AliveShipCount, 0);
            var instances = new List<EncounterInstanceInfo>(); Director.GetActiveInstances(instances);
            var ids = new HashSet<uint>(); foreach (var item in instances) Assert.IsTrue(ids.Add(item.Id));
        }
        // TODO(ENC-007): schedule/determinism/preconditions, defeat+loot, despawn and both pending scenario cases.
        // TODO(ENC-007): the two opt-in advisory performance fixtures retain their original records/targets.
    }
}
