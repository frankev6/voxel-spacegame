using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.App.Systems;
using Game.Core;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation.Commands;
using Game.Simulation.Common;
using Game.Simulation.Flight;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Simulation.Flight
{
    /// <summary>FLT-009: combined spawn, collider, command, propulsion and physics scenarios.</summary>
    [Category("Scenario")]
    public sealed partial class FlightScenarioTests
    {
        Scene scene;
        [SetUp] public void SetUp() => scene = SceneManager.CreateScene("FLT-009 scenarios");
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var unload = SceneManager.UnloadSceneAsync(scene); float until = Time.realtimeSinceStartup + 10;
            while (!unload.isDone && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(unload.isDone);
        }

        internal sealed class Fixture : IDisposable
        {
            readonly ContentService content;
            readonly BlueprintLibrary library;
            readonly VoxelStructureRegistry registry;
            readonly List<VoxelStructure> sources = new List<VoxelStructure>();
            public readonly EcsTestWorld Test;
            public readonly Entity ConfigEntity, InputEntity;
            public EntityManager Em => Test.EntityManager;
            public VoxelContent Content => new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            public ushort Hull => Index("block_hull");
            public ushort Thruster => Index("block_thruster_small");
            public ushort Index(string id) => content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId(id)));
            public Fixture(params Type[] extra)
            {
                content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
                library = new BlueprintLibrary(content.Catalog, Content);
                registry = VoxelStructureRegistry.CreateRuntime(library);
                var systems = new List<Type>
                {
                    typeof(GridSpawnSystem), typeof(GridSpawnBodyInitSystem), typeof(GridBodyInitSystem), typeof(GridMassUpdateSystem),
                    typeof(BrickDirtyScheduleSystem), typeof(ColliderSnapshotSystem), typeof(BrickColliderBuildSystem), typeof(ColliderSwapSystem),
                    typeof(GridColliderCleanupSystem), typeof(DeferredDisposalSystem), typeof(GameplayEndCommandBufferSystem),
                    typeof(ThrusterInitSystem), typeof(PlayerCommandApplySystem), typeof(PropulsionSystem), typeof(PlanarConstraintSystem),
                    typeof(SimClockAdvanceSystem), typeof(EdgeCountSystem)
                };
                systems.AddRange(extra);
                foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                    if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics") || type.Namespace == "Unity.Transforms")) systems.Add(type);
                Test = EcsTestWorld.Create(systems.ToArray()); Test.AddGameplayActive(); Test.AddSimClock(); content.CreateSingletons(Em);
                Em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                Em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
                var physics = content.Catalog.Get<PhysicsVoxelConfigDefinition>(new ContentId("config_physics"));
                Em.CreateSingleton(PhysicsVoxelConfigFactory.Create(physics)); Em.CreateSingleton(PhysicsVoxelConfigFactory.CreatePhysicsStep(physics));
                ConfigEntity = Em.CreateSingleton(FlightConfig.Default); InputEntity = Em.CreateSingleton(default(PlayerCommand));
            }
            // Synthetic physics fixture only: no art asset or player ship design is created.
            VoxelStructure Build(int aftThrusters, float cellSize)
            {
                var source = new VoxelStructure(cellSize);
                for (int z = 0; z < 8; z++) for (int x = z == 0 ? -6 : -4; x <= (z == 0 ? 6 : 4); x++)
                    Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = Hull, Origin = new int3(x, 0, z) }, Content));
                for (int i = 0; i < aftThrusters; i++)
                    Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement
                    {
                        Block = Thruster, Origin = new int3(i == 0 ? 6 : -6, 0, -1), Orient = content.Orientations.Value.AxisTurn(1, 2)
                    }, Content));
                return source;
            }
            public Entity Spawn(int aftThrusters = 2, float3 position = default, float3 velocity = default,
                bool exactThreeMetreOffset = false, GridProfile profile = GridProfile.Dummy)
            {
                float cellSize = .5f;
                if (exactThreeMetreOffset)
                {
                    using var probe = Build(1, 1);
                    var mass = probe.GetMass(Content); var instance = probe.Data.Instances[0];
                    ref var def = ref content.BlockDefs.Value.Blocks[instance.Block];
                    float3 point = GridSpaceMath.InstanceCenter(ref def, ref content.Orientations.Value, instance.Orient, instance.Origin, 1);
                    cellSize = 3 / (point.x - (float)(mass.FirstMoment.x / mass.Mass));
                }
                var source = Build(aftThrusters, cellSize);
                return SpawnSource(source, position, velocity, profile);
            }
            public Entity SpawnSource(VoxelStructure source, float3 position = default, float3 velocity = default, GridProfile profile = GridProfile.Dummy)
            {
                sources.Add(source);
                return GridSpawnApi.RequestSpawn(Em, registry, registry.Register(source), new GridSpawnParams
                {
                    Pose = new RigidTransform(quaternion.identity, position), Motion = GridMotion.Dynamic,
                    Flags = GridSpawnFlags.NoRender | GridSpawnFlags.Transient, Profile = profile, LinearVelocity = velocity
                });
            }
            public Entity SpawnBlueprint(ShipBlueprintDefinition definition, float3 position = default)
            {
                Assert.IsTrue(library.TryGet(definition.Id.Hash, out var source), definition.IdString);
                return SpawnSource(source.Clone(), position);
            }
            public void Tick(int count = 1) => Test.StepTicks(count);
            public void Ready(params Entity[] roots)
            {
                for (int frame = 0; frame < 30; frame++)
                {
                    Tick(); bool ready = true;
                    foreach (var root in roots)
                        ready &= Em.HasComponent<GridColliderState>(root) && Em.GetComponentData<GridColliderState>(root).Phase == GridColliderPhase.Ready &&
                            !Em.IsComponentEnabled<GridSpawning>(root) && Em.GetComponentData<FlightState>(root).Flags.HasFlag(FlightFlags.Initialized);
                    if (ready) return;
                }
                Assert.Fail("Flight roots not ready after 30 frames");
            }
            public void Command(Entity root, float2 thrust = default, float turn = 0)
                => Em.SetComponentData(root, new ShipCommand { Thrust = thrust, Turn = turn });
            public PhysicsVelocity Velocity(Entity root) => Em.GetComponentData<PhysicsVelocity>(root);
            public PhysicsMass Mass(Entity root) => Em.GetComponentData<PhysicsMass>(root);
            public LocalTransform Transform(Entity root) => Em.GetComponentData<LocalTransform>(root);
            public void RemoveThruster(Entity root, int index)
            {
                var instance = Em.GetBuffer<BlockInstance>(root)[index];
                ref var def = ref content.BlockDefs.Value.Blocks[instance.Block];
                var grid = Em.GetComponentData<VoxelGrid>(root); grid.Revision++;
                for (int i = 0; i < def.Cells.Length; i++)
                {
                    int3 cell = instance.Origin + content.Orientations.Value.Rotate(def.Cells[i], instance.Orient);
                    var brickEntity = Em.GetBuffer<BrickSlot>(root)[BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims)].Brick;
                    var brick = Em.GetComponentData<Brick>(brickEntity); var dirty = Em.GetComponentData<BrickDirty>(brickEntity);
                    Assert.IsTrue(BrickWrite.Set(ref brick, Em.GetBuffer<BrickCells>(brickEntity), Em.GetBuffer<BrickSparseCells>(brickEntity),
                        Em.GetBuffer<BrickSolidMask>(brickEntity), ref dirty, BrickMath.LocalIndex(BrickMath.LocalOf(cell)), default, grid.Revision));
                    Em.SetComponentData(brickEntity, brick); Em.SetComponentData(brickEntity, dirty); Em.SetComponentEnabled<BrickDirty>(brickEntity, true);
                }
                // M2 DamageResolveSystem owns these production writes; this scenario supplies its specified removal result.
                Em.DestroyEntity(instance.Functional); instance.Functional = Entity.Null; instance.Flags |= BlockInstanceFlags.Destroyed;
                instance.CellsAlive = 0; instance.FunctionHp = 0;
                var instances = Em.GetBuffer<BlockInstance>(root); instances[index] = instance;
                Em.SetComponentData(root, grid); Em.SetComponentEnabled<GridMassRecompute>(root, true);
            }
            public void Dispose()
            {
                Test.Dispose(); Object.DestroyImmediate(registry);
                foreach (var source in sources) source.Dispose(); library.Dispose(); content.Dispose();
            }
        }

        [Test]
        public void OffCentreThruster_Yaws()
        {
            using var f = new Fixture(); var config = FlightConfig.Default; config.CoreMaxYawTorque = 0; f.Em.SetComponentData(f.ConfigEntity, config);
            var root = f.Spawn(1, exactThreeMetreOffset: true); f.Ready(root);
            var thruster = f.Em.GetBuffer<FlightThruster>(root)[0]; var mass = f.Mass(root);
            float lever = thruster.PositionLocal.x - mass.CenterOfMass.x;
            Assert.That(lever, Is.EqualTo(3).Within(.00001f));
            float sign = math.sign(math.cross(thruster.PositionLocal - mass.CenterOfMass, thruster.ForceDirLocal).y);
            f.Command(root, new float2(0, 1)); f.Tick();
            Assert.Greater(sign * f.Velocity(root).Angular.y, .00001f);
            Assert.AreEqual(0, f.Em.GetComponentData<FlightState>(root).GyroTorque);
            Assert.Greater(f.Em.GetBuffer<FlightThruster>(root)[0].Throttle, 0);
            TestContext.WriteLine($"Off-centre thrust: lever {lever:G8} m, yaw {f.Velocity(root).Angular.y:G8} rad/s");
        }

        [Test]
        public void Ram_PushesShips()
        {
            using var f = new Fixture();
            var a = f.Spawn(0, position: new float3(0, 0, -12), velocity: new float3(0, 0, 30)); var b = f.Spawn(0);
            f.Ready(a, b); double ma = 1 / f.Mass(a).InverseMass, mb = 1 / f.Mass(b).InverseMass;
            double3 before = ma * (double3)f.Velocity(a).Linear + mb * (double3)f.Velocity(b).Linear;
            f.Tick(60); var va = f.Velocity(a).Linear; var vb = f.Velocity(b).Linear;
            Assert.Less(va.z, 29); Assert.Greater(vb.z, 1);
            double error = math.length(ma * (double3)va + mb * (double3)vb - before) / math.length(before);
            Assert.Less(error, .03);
            Assert.Less(math.abs(f.Transform(a).Position.y), .001f); Assert.Less(math.abs(f.Transform(b).Position.y), .001f);
            TestContext.WriteLine($"Ram: A {va}, B {vb}; momentum error {error:G8}");
        }

        [Test]
        public void LostThruster_ChangesHandling()
        {
            using var f = new Fixture(); var root = f.Spawn(); f.Ready(root);
            f.Command(root, new float2(0, 1)); f.Tick(12);
            float beforeAcceleration = math.length(f.Velocity(root).Linear) / (12 * SimTime.FixedTimestep);
            f.Command(root); f.RemoveThruster(root, 0); f.Tick(2);
            Assert.AreEqual(1, f.Em.GetBuffer<FlightThruster>(root).Length);
            // Keep the initial pose for the prediction: the allocator's force/torque are in that body frame.
            var before = f.Velocity(root); var transform = f.Transform(root); var mass = f.Mass(root);
            f.Command(root, new float2(0, 1)); f.Tick();
            var flight = f.Em.GetComponentData<FlightState>(root); var thruster = f.Em.GetBuffer<FlightThruster>(root)[0];
            float torque = math.cross(thruster.PositionLocal - mass.CenterOfMass, thruster.ForceDirLocal * thruster.MaxForce * thruster.Throttle).y;
            float gyro = flight.Gyro * flight.GyroTorque;
            float expected = before.Angular.y + (torque + gyro) * mass.InverseInertia.y * SimTime.FixedTimestep;
            Assert.That(f.Velocity(root).Angular.y, Is.EqualTo(expected).Within(.0001f));
            Assert.LessOrEqual(math.abs(gyro), flight.GyroTorque + .001f);
            Assert.Less(math.abs(torque + gyro), math.abs(torque));
            float forwardAcceleration = math.dot(f.Velocity(root).Linear - before.Linear, math.rotate(transform.Rotation, new float3(0, 0, 1))) / SimTime.FixedTimestep;
            Assert.Greater(forwardAcceleration, 0); Assert.Less(forwardAcceleration, beforeAcceleration);
            TestContext.WriteLine($"Lost thruster: acceleration {beforeAcceleration:G8} -> {forwardAcceleration:G8}; thrust torque {torque:G8}, gyro {gyro:G8}, yaw {expected:G8}");
        }

        [Test]
        public void AftOnly_TurnsInPlace()
        {
            using var f = new Fixture(); var root = f.Spawn(); f.Ready(root);
            f.Command(root, turn: 1); f.Tick(180);
            var flight = f.Em.GetComponentData<FlightState>(root);
            Assert.That(f.Velocity(root).Angular.y, Is.EqualTo(flight.YawRateCap).Within(flight.YawRateCap * .01f));
            Assert.Greater(flight.GyroTorque, 0);
            TestContext.WriteLine($"Aft-only turn: yaw {f.Velocity(root).Angular.y:G8}, cap {flight.YawRateCap:G8}");
        }

        [Test]
        public void Edges_FrameRateIndependent()
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                using var f = new Fixture(); var root = f.Spawn(profile: GridProfile.PlayerShip); f.Ready(root);
                f.Em.AddComponent<ScenarioEdges>(root);
                for (int frame = 0; frame < fps * 2; frame++)
                {
                    byte presses = (byte)System.Math.Min(8, (int)System.Math.Floor((frame + 1) * 5.0 / fps));
                    f.Em.SetComponentData(f.InputEntity, new PlayerCommand { Edges = new PlayerEdgeCounters { Ability1 = presses } });
                    f.Test.StepFrame(1f / fps);
                }
                f.Tick(); Assert.AreEqual(8, f.Em.GetComponentData<ScenarioEdges>(root).Count, $"{fps} FPS");
            }
        }

        public struct ScenarioEdges : IComponentData { public int Count; }
        [DisableAutoCreation]
        [UpdateInGroup(typeof(ShipCommandGroup))]
        [UpdateAfter(typeof(PlayerCommandApplySystem))]
        public partial struct EdgeCountSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                foreach (var (command, edges) in SystemAPI.Query<RefRO<ShipCommand>, RefRW<ScenarioEdges>>())
                    if ((command.ValueRO.Edges & ShipCommandEdges.Ability1) != 0) edges.ValueRW.Count++;
            }
        }
    }
}
