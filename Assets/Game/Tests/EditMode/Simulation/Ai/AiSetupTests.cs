using System;
using Game.Core;
using Game.Core.Components;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>AI-005: profile lookup, real request conversion, and the four SPEC-23 §5.3 anchor/sequence cases.</summary>
    public sealed class AiSetupTests
    {
        NativeLeakDetectionMode leakMode;
        bool burstSafety;

        [SetUp] public void SetUp()
        {
            leakMode = NativeLeakDetection.Mode;
            burstSafety = BurstCompiler.Options.EnableBurstSafetyChecks;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            BurstCompiler.Options.EnableBurstSafetyChecks = true;
        }

        [TearDown] public void TearDown()
        {
            DestroyAll();
            BurstCompiler.Options.EnableBurstSafetyChecks = burstSafety;
            NativeLeakDetection.Mode = leakMode;
        }

        [Test] public void UnknownProfile_Default()
        {
            var first = Create<AiProfileDefinition>("ai_default");
            var selected = Create<AiProfileDefinition>("ai_selected");
            var report = new ValidationReport("AI setup");
            var catalog = ContentCatalog.Build(new ContentDefinition[]
            {
                Block("block_missing", BlockKind.Structural), Item("item_missing"), selected, first
            }, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            Assert.AreEqual(0, AiSetup.ProfileIndexOf(catalog, ContentId.None));
            Assert.AreEqual(1, AiSetup.ProfileIndexOf(catalog, first.Id));
            Assert.AreEqual(2, AiSetup.ProfileIndexOf(catalog, selected.Id), "The catalog uses one-based family indices, not source order.");
            var previousLevel = GameLog.GetMinimumLevel(LogCategories.AI);
            try
            {
                GameLog.SetMinimumLevel(LogCategories.AI, LogLevel.Warning);
                GameLog.ResetOnceKeys();
                LogAssert.Expect(LogType.Warning, "[AI] Unknown AI profile 'ai_unknown_setup'; using the default profile.");
                for (int i = 0; i < 3; i++) Assert.AreEqual(0, AiSetup.ProfileIndexOf(catalog, new ContentId("ai_unknown_setup")));
                LogAssert.Expect(LogType.Warning, "[AI] Unknown AI profile 'block_missing'; using the default profile.");
                Assert.AreEqual(0, AiSetup.ProfileIndexOf(catalog, new ContentId("block_missing")), "Another content family is not an AI profile.");
                Assert.AreEqual(0, AiSetup.ProfileIndexOf(catalog, ContentId.None));
                LogAssert.NoUnexpectedReceived();
            }
            finally { GameLog.SetMinimumLevel(LogCategories.AI, previousLevel); GameLog.ResetOnceKeys(); }
        }

        [Test] public void Anchor_FromPose()
        {
            using var content = new VoxelTestContent();
            using var library = new BlueprintLibrary(content.Service.Catalog, content.Content);
            using var source = new VoxelStructure();
            ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = content.Index("block_hull") });
            source.SetBrickUniform(int3.zero, Cell.Palette(palette));
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                using var world = new World("AI setup request conversion");
                var em = world.EntityManager;
                content.Service.CreateSingletons(em);
                em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 128, DefaultCellSize = .5f });
                em.CreateEntity(typeof(GameplayActive));
                em.CreateSingleton(new OriginShiftRecord { Sequence = 7, Delta = new float3(-2000, 0, 1000) });
                var spawn = world.CreateSystem<GridSpawnSystem>();
                var anchor = world.CreateSystem<AiAnchorShiftSystem>();
                using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<RootComponentRegistration>()))
                {
                    var registrations = query.GetSingletonBuffer<RootComponentRegistration>(true);
                    Assert.AreEqual(3, registrations.Length);
                    for (int i = 0; i < registrations.Length; i++) Assert.AreEqual(ComponentType.ReadWrite<AiBrain>(), registrations[i].Type);
                    CollectionAssert.AreEquivalent(new[] { GridProfile.NpcShip, GridProfile.Station, GridProfile.SmallCraft },
                        new[] { registrations[0].Profile, registrations[1].Profile, registrations[2].Profile });
                }
                int handle = registry.Register(source);
                var roots = new Entity[6];
                for (GridProfile profile = GridProfile.None; profile <= GridProfile.Dummy; profile++)
                {
                    var root = GridSpawnApi.RequestSpawn(em, registry, handle, new GridSpawnParams
                    {
                        PersistentId = (ulong)profile + 1, Profile = profile, Motion = GridMotion.Dynamic,
                        Pose = new RigidTransform(quaternion.RotateY(.7f), new float3((int)profile * 31, 4, -80)), Flags = GridSpawnFlags.NoRender
                    });
                    roots[(int)profile] = root;
                    if (profile == GridProfile.NpcShip) em.AddComponentData(root, new AiBrain { ProfileIndex = 3 });
                }
                spawn.Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                for (GridProfile profile = GridProfile.None; profile <= GridProfile.Dummy; profile++)
                {
                    bool expected = profile == GridProfile.NpcShip || profile == GridProfile.Station || profile == GridProfile.SmallCraft;
                    Assert.AreEqual(expected, em.HasComponent<AiBrain>(roots[(int)profile]), profile.ToString());
                }
                Assert.AreEqual(3, em.GetComponentData<AiBrain>(roots[(int)GridProfile.NpcShip]).ProfileIndex, "Conversion preserves requester values.");
                Entity player = roots[(int)GridProfile.PlayerShip], craft = roots[(int)GridProfile.SmallCraft];
                em.AddComponent<PlayerShip>(player);
                em.AddComponentData(player, new AiBrain { ProfileIndex = 9 });
                em.AddComponent<Disabled>(craft); // Stowed brains initialise from their live pose only after re-enabling.
                anchor.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                foreach (GridProfile profile in new[] { GridProfile.NpcShip, GridProfile.Station })
                {
                    Entity root = roots[(int)profile];
                    AssertInitialized(em.GetComponentData<AiBrain>(root), em.GetComponentData<LocalTransform>(root), 7, profile == GridProfile.NpcShip ? (ushort)3 : (ushort)0);
                }
                Assert.AreEqual(0, em.GetComponentData<AiBrain>(player).Initialized, "An accidental player brain is excluded.");
                Assert.AreEqual(0, em.GetComponentData<AiBrain>(craft).Initialized, "Disabled/stowed grids are excluded.");
                em.RemoveComponent<Disabled>(craft);
                anchor.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                AssertInitialized(em.GetComponentData<AiBrain>(craft), em.GetComponentData<LocalTransform>(craft), 7, 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(registry); }
        }

        [Test] public void Shift_AppliedOnce()
        {
            using var f = new AnchorFixture(new OriginShiftRecord { Sequence = 5 });
            float3 start = new float3(13, 2, -45), goal = new float3(90, 2, 35), delta = new float3(-2000, -2, 1000);
            Entity ship = f.Ship(start, new AiBrain { ProfileIndex = 2 });
            f.Tick();
            var brain = f.Em.GetComponentData<AiBrain>(ship);
            brain.GoalPosition = goal; brain.State = AiState.Engage; brain.ThinkCount = 11; brain.GoalSpeedFraction = .8f;
            f.Em.SetComponentData(ship, brain);
            f.Em.SetComponentData(ship, LocalTransform.FromPosition(start + delta));
            f.Record(new OriginShiftRecord { Sequence = 6, Delta = delta });
            Entity late = f.Ship(new float3(50, 0, 60), new AiBrain { ProfileIndex = 3 });
            f.Tick();
            brain = f.Em.GetComponentData<AiBrain>(ship);
            Assert.AreEqual(start + delta, brain.Anchor); Assert.AreEqual(goal + delta, brain.GoalPosition);
            Assert.AreEqual(6u, brain.ShiftSequence); Assert.AreEqual(2, brain.ProfileIndex); Assert.AreEqual(AiState.Engage, brain.State);
            Assert.AreEqual(11, brain.ThinkCount); Assert.AreEqual(.8f, brain.GoalSpeedFraction);
            AssertInitialized(f.Em.GetComponentData<AiBrain>(late), f.Em.GetComponentData<LocalTransform>(late), 6, 3);
            f.Tick();
            Assert.AreEqual(brain.Anchor, f.Em.GetComponentData<AiBrain>(ship).Anchor);
            Assert.AreEqual(brain.GoalPosition, f.Em.GetComponentData<AiBrain>(ship).GoalPosition);

            float3 current = new float3(-30, 6, 70);
            f.Em.SetComponentData(ship, LocalTransform.FromPosition(current));
            f.Record(new OriginShiftRecord { Sequence = 8, Delta = new float3(9000, 0, 0) });
            f.Tick();
            brain = f.Em.GetComponentData<AiBrain>(ship);
            Assert.AreEqual(current, brain.Anchor); Assert.AreEqual(current, brain.GoalPosition, "A missed rebase resets unrecoverable coordinates.");
            Assert.AreEqual(8u, brain.ShiftSequence);
            brain.ShiftSequence = uint.MaxValue; brain.Anchor = start; brain.GoalPosition = goal;
            f.Em.SetComponentData(ship, brain);
            f.Record(new OriginShiftRecord { Sequence = 0, Delta = delta });
            f.Tick();
            Assert.AreEqual(start + delta, f.Em.GetComponentData<AiBrain>(ship).Anchor, "Unsigned sequence wrap is an adjacent shift.");
            Assert.AreEqual(goal + delta, f.Em.GetComponentData<AiBrain>(ship).GoalPosition);
            f.Tick();
            Assert.AreEqual(start + delta, f.Em.GetComponentData<AiBrain>(ship).Anchor, "Wrapped sequence is still applied once.");
        }

        [Test] public void StarSystemChange_ResetsAnchor()
        {
            using var f = new AnchorFixture(new OriginShiftRecord { Sequence = 10 });
            float3 position = new float3(24, 3, -80);
            Entity ship = f.Ship(position, new AiBrain
            {
                Initialized = 1, ShiftSequence = 10, ProfileIndex = 2, State = AiState.Return, OrbitSign = -1,
                Anchor = new float3(4000, 0, 8000), GoalPosition = new float3(-5000, 0, -9000), GoalHeadingYaw = .4f, GoalSpeedFraction = .8f
            });
            f.Record(new OriginShiftRecord { Sequence = 11, StarSystemChanged = 1 });
            f.Tick();
            var brain = f.Em.GetComponentData<AiBrain>(ship);
            Assert.AreEqual(position, brain.Anchor); Assert.AreEqual(position, brain.GoalPosition); Assert.AreEqual(11u, brain.ShiftSequence);
            Assert.AreEqual(2, brain.ProfileIndex); Assert.AreEqual(AiState.Return, brain.State); Assert.AreEqual(-1, brain.OrbitSign);
            Assert.AreEqual(.4f, brain.GoalHeadingYaw); Assert.AreEqual(.8f, brain.GoalSpeedFraction);
            f.Em.SetComponentData(ship, LocalTransform.FromPosition(position + new float3(50, 0, 0)));
            f.Tick();
            Assert.AreEqual(position, f.Em.GetComponentData<AiBrain>(ship).Anchor, "The retained system-change flag must not reset every tick.");
            Assert.AreEqual(position, f.Em.GetComponentData<AiBrain>(ship).GoalPosition);
        }

        static void AssertInitialized(AiBrain brain, LocalTransform pose, uint sequence, ushort profileIndex)
        {
            Assert.AreEqual(1, brain.Initialized); Assert.AreEqual(AiState.Idle, brain.State); Assert.AreEqual(1, brain.OrbitSign);
            Assert.AreEqual(pose.Position, brain.Anchor); Assert.AreEqual(pose.Position, brain.GoalPosition);
            Assert.That(brain.GoalHeadingYaw, Is.EqualTo(Planar.YawFromForward(math.rotate(pose.Rotation, new float3(0, 0, 1)))).Within(1e-6f));
            Assert.AreEqual(sequence, brain.ShiftSequence); Assert.AreEqual(profileIndex, brain.ProfileIndex);
        }

        sealed class AnchorFixture : IDisposable
        {
            readonly World world = new World("AI anchor shift");
            readonly Entity shift;
            readonly SystemHandle system;
            public EntityManager Em => world.EntityManager;
            public AnchorFixture(OriginShiftRecord record)
            {
                Em.AddBuffer<RootComponentRegistration>(Em.CreateEntity());
                Em.CreateEntity(typeof(GameplayActive));
                shift = Em.CreateSingleton(record);
                system = world.CreateSystem<AiAnchorShiftSystem>();
            }
            public Entity Ship(float3 position, AiBrain brain)
            {
                var entity = Em.CreateEntity(typeof(AiBrain), typeof(LocalTransform));
                Em.SetComponentData(entity, brain);
                Em.SetComponentData(entity, LocalTransform.FromPositionRotation(position, quaternion.RotateY(.3f)));
                return entity;
            }
            public void Record(OriginShiftRecord record) => Em.SetComponentData(shift, record);
            public void Tick() { system.Update(world.Unmanaged); Em.CompleteAllTrackedJobs(); }
            public void Dispose() { Em.CompleteAllTrackedJobs(); world.Dispose(); }
        }
    }
}
