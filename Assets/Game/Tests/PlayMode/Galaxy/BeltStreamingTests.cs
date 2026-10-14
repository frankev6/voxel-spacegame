using System;
using System.Collections;
using System.Linq;
using Game.Core.Components;
using Game.Simulation.Commands;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Streaming;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class BeltStreamingTests
    {
        sealed class Fixture : IDisposable
        {
            internal readonly StarSystemLoadTests.Fixture F = new(true);
            internal readonly BodySpec Belt;
            internal readonly BodyTemplateDefinition Template;
            internal double3 Position;
            internal Fixture()
            {
                var templates = (BodyTemplateDefinition[])F.Galaxy.Galaxy.BodyTemplates;
                int index = Array.FindIndex(templates, x => x.Role == BodyRole.Belt);
                Assert.GreaterOrEqual(index, 0);
                Template = Object.Instantiate(templates[index]);
                using (var fields = new SerializedObject(Template))
                {
                    fields.FindProperty("sectorSizeM").floatValue = 50;
                    fields.FindProperty("asteroidsPerSectorMin").intValue = 2;
                    fields.FindProperty("asteroidsPerSectorMax").intValue = 2;
                    fields.FindProperty("asteroidRadiusMinM").floatValue = .5f;
                    fields.FindProperty("asteroidRadiusMaxM").floatValue = .5f;
                    fields.FindProperty("asteroidNoiseAmplitude").floatValue = 0;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
                // Generation is already complete. Override only this fixture's runtime template array.
                templates[index] = Template;
                Belt = new BodySpec { Id = new BodyId(F.State.CurrentStarSystem, BodyRole.Belt, 0, 0),
                    TemplateIndex = (ushort)index, BeltInnerRadiusM = 0, BeltOuterRadiusM = 50, Seed = 73 };
                var content = F.Galaxy.GetContent(F.State.CurrentStarSystem);
                content.Bodies.Clear(); content.Bodies.Add(Belt);
                using var settings = F.Em.CreateEntityQuery(typeof(StreamingConfig));
                settings.SetSingleton(new StreamingConfig { T2UnloadFactor = 1.25f, BubbleRadiusM = 10,
                    BubbleUnloadFactor = 1.25f, MaxGridSpawnsPerFrame = 1, MaxAsteroidJobsInFlight = 2,
                    MaxAsteroidsPerSector = 8, PersistDamageMaxCells = 64 });
                Position = Centre(0);
                F.Load();
                F.Player = F.Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform));
            }
            internal double3 Centre(int sector) => BeltSectorGeometry.Centre(Belt, 50, sector);
            internal Entity[] Roots()
            {
                using var query = F.Em.CreateEntityQuery(typeof(BodyGridOwner), typeof(VoxelGrid));
                using var values = query.ToEntityArray(Allocator.Temp);
                return values.ToArray();
            }
            internal void TickLoader()
            {
                using var origin = F.Em.CreateEntityQuery(typeof(FrameOrigin));
                F.Em.SetComponentData(F.Player, LocalTransform.FromPosition((float3)(Position - origin.GetSingleton<FrameOrigin>().OriginInStarSystem)));
                F.Tick();
            }
            internal void Step()
            {
                TickLoader(); F.Spawn.Update(F.World.Unmanaged); F.Em.CompleteAllTrackedJobs();
            }
            internal IEnumerator Until(Func<bool> ready)
            {
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 2;
                while (!ready())
                {
                    Assert.Less(UnityEditor.EditorApplication.timeSinceStartup, deadline, "Small belt fixture did not reach the requested state.");
                    Step(); yield return null;
                }
            }
            public void Dispose() { F.Dispose(); Object.DestroyImmediate(Template); }
        }
        [Test] public void SectorDeterministic()
        {
            using var f = new Fixture();
            using var first = new NativeList<AsteroidSpawnSpec>(8, Allocator.TempJob);
            using var second = new NativeList<AsteroidSpawnSpec>(8, Allocator.TempJob);
            var job = new AsteroidSectorJob { Belt = f.Belt, Sector = 0, MinCount = 2, MaxCount = 2,
                SectorSizeM = 50, MinRadiusM = .5f, MaxRadiusM = .5f, Output = first };
            job.Schedule().Complete(); job.Output = second; job.Schedule().Complete();
            Assert.AreEqual(2, first.Length); Assert.AreEqual(first.Length, second.Length);
            var content = new VoxelContent { Blocks = f.F.VoxelContent.BlockDefs, Orientations = f.F.VoxelContent.Orientations };
            var builder = new AsteroidBuilder(f.F.Catalog, content);
            var names = Game.Voxel.Data.VoxsBlockNames.FromCatalog(f.F.Catalog);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].Id, second[i].Id); Assert.AreEqual(first[i].PositionM, second[i].PositionM);
                Assert.AreEqual(first[i].RadiusM, second[i].RadiusM); Assert.AreEqual(first[i].YawRad, second[i].YawRad);
                using var a = builder.Build(first[i].RadiusM, .5f, f.Template.AsteroidBlock, 0, .15f, first[i].ShapeSeed);
                using var b = builder.Build(second[i].RadiusM, .5f, f.Template.AsteroidBlock, 0, .15f, second[i].ShapeSeed);
                Assert.AreEqual(a.ComputeContentHash(names), b.ComputeContentHash(names));
            }
        }
        [UnityTest] public IEnumerator DestroyedNeverReturns()
        {
            using var f = new Fixture(); yield return f.Until(() => f.Roots().Length == 2);
            Entity root = f.Roots()[0]; var id = f.F.Em.GetComponentData<BodyGridOwner>(root).Body;
            // Feed the mass owner's depletion result to the actual body watcher and loader event path.
            if (!f.F.Em.HasComponent<GridMass>(root)) f.F.Em.AddComponent<GridMass>(root);
            var mass = f.F.Em.GetComponentData<GridMass>(root); mass.Data.CellCount = 0; f.F.Em.SetComponentData(root, mass);
            f.F.Watch.Update(f.F.World.Unmanaged); f.F.Em.CompleteAllTrackedJobs(); f.Step();
            CollectionAssert.Contains(f.F.State.GetOrCreateDelta(id.System).Destroyed, id.LocalKey);
            yield return f.Until(() => f.Roots().Length == 1);
            f.Position = new double3(1000, 0, 1000); yield return f.Until(() => f.Roots().Length == 0);
            f.Position = f.Centre(0); yield return f.Until(() => f.Roots().Length == 1);
            Assert.AreNotEqual(id, f.F.Em.GetComponentData<BodyGridOwner>(f.Roots()[0]).Body);
        }
        [UnityTest] public IEnumerator BubbleBound()
        {
            using var f = new Fixture(); yield return f.Until(() => f.Roots().Length == 2);
            f.Position = f.Centre(2);
            yield return f.Until(() => f.Roots().Length == 2 && f.Roots().All(x => f.F.Em.GetComponentData<BodyGridOwner>(x).Body.SubSlot / 8 == 2));
            foreach (var root in f.Roots())
            {
                int sector = f.F.Em.GetComponentData<BodyGridOwner>(root).Body.SubSlot / 8;
                Assert.LessOrEqual(math.distance(f.Position, f.Centre(sector)), 12.5);
            }
        }
        [UnityTest] public IEnumerator NoMainThreadGeneration()
        {
            using var f = new Fixture();
            f.TickLoader(); // Schedules descriptors; no same-tick synchronous build or grid creation.
            Assert.AreEqual(0, f.Roots().Length);
            using (var requests = f.F.Em.CreateEntityQuery(typeof(GridSpawnRequest))) Assert.AreEqual(0, requests.CalculateEntityCount());
            f.TickLoader(); // At most advances the completed descriptor job to a scheduled shape job.
            Assert.AreEqual(0, f.Roots().Length);
            yield return f.Until(() => f.Roots().Length == 2);
        }
    }
}
