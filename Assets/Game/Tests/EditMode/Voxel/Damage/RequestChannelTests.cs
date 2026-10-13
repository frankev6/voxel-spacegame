using Game.Data.Combat;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Damage
{
    /// <summary>SPEC-13 DMG-019: production request channels, impulse writer and restore resolver.</summary>
    public unsafe sealed class RequestChannelTests
    {
        [Test] public void Impulse_ChangesVelocity()
        {
            using var f = new ImpactEventSystemTests.Fixture();
            var body = f.Body(0, 0, mass: 1000);
            var streams = f.Test.GetSingleton<DamageStreams>();
            streams.Impulses.PostFromMainThread(new ImpulseRequest
            {
                Body = body, WorldImpulse = new float3(1000, 0, 0),
                Flags = ImpulseRequestFlags.AtCenterOfMass, Producer = ProducerKind.Projectile
            });
            f.Tick();
            var velocity = f.EM.GetComponentData<PhysicsVelocity>(body);
            Assert.That(velocity.Linear.x, Is.EqualTo(1).Within(1e-6f));
            Assert.AreEqual(float3.zero, velocity.Angular);
            Assert.AreEqual(1, streams.Data->Current.ImpulsesApplied);
            f.Tick();
            Assert.That(f.EM.GetComponentData<PhysicsVelocity>(body).Linear.x, Is.EqualTo(1).Within(1e-6f), "Request is consumed once");
        }

        [Test] public void Impulse_PlanarClamp()
        {
            using var f = new ImpactEventSystemTests.Fixture();
            var body = f.Body(0, 0, mass: 1000);
            f.EM.AddComponent<Game.Data.Components.PlanarConstraint>(body);
            var streams = f.Test.GetSingleton<DamageStreams>();
            streams.Impulses.PostFromMainThread(new ImpulseRequest
            {
                Body = body, WorldImpulse = new float3(1000, 2000, 3000),
                WorldPoint = new float3(1, 2, 3), Producer = ProducerKind.Projectile
            });
            f.Tick(); var velocity = f.EM.GetComponentData<PhysicsVelocity>(body);
            Assert.That(math.distance(new float3(1, 0, 3), velocity.Linear), Is.LessThan(1e-6f));
            Assert.AreEqual(0, velocity.Angular.x); Assert.AreEqual(0, velocity.Angular.z);
            Assert.Greater(math.abs(velocity.Angular.y), 0);
        }

        [Test] public void Impulse_OrderIndependent()
        {
            PhysicsVelocity? expected = null;
            foreach (bool reverse in new[] { false, true })
            {
                using var f = new ImpactEventSystemTests.Fixture();
                var body = f.Body(0, 0, mass: 1000);
                var streams = f.Test.GetSingleton<DamageStreams>();
                // Cancellation makes accumulation order observable; sequence must control it.
                var impulses = new[] { new float3(1e10f, 0, 0), new float3(-1e10f, 0, 0), new float3(1000, 0, 0) };
                for (int n = 0; n < impulses.Length; n++)
                {
                    int i = reverse ? impulses.Length - 1 - n : n;
                    streams.Impulses.PostFromMainThread(new ImpulseRequest
                    { Body = body, WorldImpulse = impulses[i], WorldPoint = new float3(0, 0, 1), Sequence = (uint)i, Producer = ProducerKind.Projectile });
                }
                f.Tick(); var actual = f.EM.GetComponentData<PhysicsVelocity>(body);
                Assert.AreEqual(3, streams.Data->Current.ImpulsesApplied);
                if (expected.HasValue) { Assert.AreEqual(expected.Value.Linear, actual.Linear); Assert.AreEqual(expected.Value.Angular, actual.Angular); }
                else expected = actual;
            }
        }

        static CellRestoreRequest Heal(Entity root, int3 min, int3 max) => new CellRestoreRequest
        { Grid = root, Mode = CellRestoreMode.Heal, CellMin = min, CellMax = max, HpFraction = .5f, Producer = ProducerKind.Projectile };
        static CellRestoreRequest Place(DamageResolveSystemTests.Fixture f, Entity root, int3 position) => new CellRestoreRequest
        { Grid = root, Mode = CellRestoreMode.Place, CellMin = position, Block = f.Grid.Content.Index("block_hull"), Color = 7, Producer = ProducerKind.Projectile };
        static void SetHp(DamageResolveSystemTests.Fixture f, Entity root, int3 position, byte hp)
        {
            using var writer = f.Grid.Writer(root);
            Assert.IsTrue(writer.Access.TryGetCell(position, out var cell, out var brick, out int local));
            cell.Hp = hp; Assert.IsTrue(writer.Access.Write(brick, local, cell));
        }

        static Cell ReadCell(DamageResolveSystemTests.Fixture f, Entity root, int3 position)
        {
            using var writer = f.Grid.Writer(root);
            return writer.Access.TryGetCell(position, out var cell, out _, out _) ? cell : default;
        }

        [Test] public void Heal_Box()
        {
            using var f = new DamageResolveSystemTests.Fixture();
            var root = f.Grid.Spawn(f.Grid.CoreAndReactor());
            for (int x = -1; x <= 1; x++) SetHp(f, root, new int3(x, 1, 0), x == 1 ? (byte)200 : (byte)40);
            SetHp(f, root, 0, 40);
            int index = ReadCell(f, root, 0).InstanceIndex;
            var instances = f.Grid.EM.GetBuffer<BlockInstance>(root); var instance = instances[index];
            float maximum = f.Grid.Content.Service.BlockDefs.Value.Blocks[instance.Block].FunctionHp;
            instance.FunctionHp = maximum * .1f; instances[index] = instance;
            // One coordinate per tick also verifies that an instance spanning a budget
            // boundary receives its functional heal only once for the request.
            using var query = f.Grid.EM.CreateEntityQuery(typeof(DamageConfig));
            var config = query.GetSingleton<DamageConfig>(); config.MaxRestoreCellsPerTick = 1; query.SetSingleton(config);
            for (uint tick = 1; tick <= 3; tick++)
            {
                f.Begin(tick);
                if (tick == 1) f.Streams.Restores.PostFromMainThread(Heal(root, new int3(-1, 1, 0), new int3(1, 1, 0)));
                f.Resolve();
            }
            for (int x = -1; x <= 1; x++) Assert.AreEqual(x == 1 ? 255 : 168, ReadCell(f, root, new int3(x, 1, 0)).Hp);
            Assert.AreEqual(40, ReadCell(f, root, 0).Hp, "Outside the inclusive heal box");
            var healed = f.Grid.EM.GetBuffer<BlockInstance>(root)[index];
            Assert.That(healed.FunctionHp, Is.EqualTo(maximum * .6f).Within(.001f));
            Assert.AreEqual(instance.Functional, healed.Functional); Assert.AreEqual(instance.CellsAlive, healed.CellsAlive);
            Assert.AreEqual(0, f.Streams.Data->Restored.Length, "Heal must not publish added mass");
        }

        [Test] public void Heal_DestroyedInstanceStays()
        {
            using var f = new DamageResolveSystemTests.Fixture(); var root = f.Grid.Spawn(f.Grid.CoreAndReactor());
            SetHp(f, root, 0, 40); int index = ReadCell(f, root, 0).InstanceIndex;
            var instances = f.Grid.EM.GetBuffer<BlockInstance>(root); var instance = instances[index];
            instance.Flags |= BlockInstanceFlags.Destroyed; instance.FunctionHp = 0; instance.Functional = Entity.Null; instances[index] = instance;
            f.Begin(1); f.Streams.Restores.PostFromMainThread(Heal(root, 0, new int3(0, 2, 0))); f.Resolve();
            var actual = f.Grid.EM.GetBuffer<BlockInstance>(root)[index];
            Assert.AreEqual(instance.Flags, actual.Flags); Assert.AreEqual(0, actual.FunctionHp);
            Assert.AreEqual(Entity.Null, actual.Functional); Assert.AreEqual(instance.CellsAlive, actual.CellsAlive);
            Assert.AreEqual(168, ReadCell(f, root, 0).Hp, "Surviving cells can heal without resurrecting the destroyed function");
        }

        [Test] public void Restore_DeterministicAcrossWorkers()
        {
            int previous = JobsUtility.JobWorkerCount; ulong[] expected = null;
            try
            {
                foreach (int workers in new[] { 1, 8 }) foreach (bool reverse in new[] { false, true })
                {
                    JobsUtility.JobWorkerCount = workers;
                    using var f = new DamageResolveSystemTests.Fixture(); var roots = new Entity[4];
                    for (int g = 0; g < roots.Length; g++)
                    {
                        roots[g] = f.Box(8);
                        for (int x = 0; x < 8; x++) SetHp(f, roots[g], new int3(x, 0, 0), 10);
                    }
                    using var query = f.Grid.EM.CreateEntityQuery(typeof(DamageConfig));
                    var config = query.GetSingleton<DamageConfig>(); config.MaxRestoreCellsPerTick = 3; query.SetSingleton(config);
                    var hashes = new ulong[24];
                    for (uint tick = 1; tick <= hashes.Length; tick++)
                    {
                        f.Begin(tick);
                        if (tick == 1) for (int n = 0; n < 8; n++)
                        {
                            int i = reverse ? 7 - n : n;
                            var request = Heal(roots[i / 2], 0, new int3(7, 0, 0));
                            request.Sequence = (uint)i; f.Streams.Restores.PostFromMainThread(request);
                        }
                        f.Resolve(); hashes[tick - 1] = CellStateHasher.ComputeWorldHash(f.Grid.EM);
                    }
                    foreach (var root in roots) for (int x = 0; x < 8; x++) Assert.AreEqual(255, f.Cell(root, x).Hp);
                    if (expected == null) expected = hashes; else CollectionAssert.AreEqual(expected, hashes, $"workers={workers}, reverse={reverse}");
                }
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }

        [Test] public void Place_AddsCell()
        {
            using var f = new DamageResolveSystemTests.Fixture(); var root = f.Box(1);
            f.Grid.CleanDirty(root); f.Begin(1);
            f.Streams.Restores.PostFromMainThread(Place(f, root, new int3(1, 0, 0))); f.Resolve();
            Assert.AreEqual(255, f.Cell(root, 1).Hp);
            Assert.AreEqual(1, f.Streams.Data->Restored.Length);
            var restored = f.Streams.Data->Restored[0]; Assert.AreEqual(root, restored.Grid);
            Assert.AreEqual(new int3(1, 0, 0), restored.Cell); Assert.AreEqual(7, restored.Color);
            Assert.AreEqual(f.Grid.Content.Index("block_hull"), restored.Block);
            var brick = f.Grid.EM.GetBuffer<BrickSlot>(root)[0].Brick;
            Assert.IsTrue(f.Grid.EM.IsComponentEnabled<BrickDirty>(brick));
            Assert.AreEqual(BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider, f.Grid.EM.GetComponentData<BrickDirty>(brick).Flags);
            Assert.AreEqual(1, f.Streams.Data->Current.CellsPlaced);
        }

        [Test] public void Place_InvalidDropped()
        {
            using var f = new DamageResolveSystemTests.Fixture();
            var source = f.Grid.Box(0, 0); source.SetCell(new int3(32, 0, 0), Cell.Palette(1));
            var root = f.Grid.Spawn(source); f.Begin(1);
            f.Streams.Restores.PostFromMainThread(Place(f, root, 0));
            f.Streams.Restores.PostFromMainThread(Place(f, root, new int3(16, 0, 0)));
            var instance = Place(f, root, new int3(1, 0, 0)); instance.Block = f.Grid.Content.Index("block_core_cross");
            f.Streams.Restores.PostFromMainThread(instance); f.Resolve();
            Assert.AreEqual(3, f.Streams.Data->Current.RestoresDropped);
            Assert.AreEqual(0, f.Streams.Data->Restored.Length);
            Assert.IsTrue(f.Cell(root, 1).IsAir); Assert.IsTrue(f.Cell(root, 16).IsAir);
        }

        [Test] public void Restore_BudgetDefers()
        {
            using var f = new DamageResolveSystemTests.Fixture(); var root = f.Box(8);
            for (int x = 0; x < 8; x++) SetHp(f, root, new int3(x, 0, 0), 10);
            using var query = f.Grid.EM.CreateEntityQuery(typeof(DamageConfig));
            var config = query.GetSingleton<DamageConfig>(); config.MaxRestoreCellsPerTick = 2; query.SetSingleton(config);
            for (uint tick = 1; tick <= 4; tick++)
            {
                f.Begin(tick);
                if (tick == 1) f.Streams.Restores.PostFromMainThread(Heal(root, 0, new int3(7, 0, 0)));
                f.Resolve();
                for (int x = 0; x < 8; x++) Assert.AreEqual(x < tick * 2 ? 138 : 10, f.Cell(root, x).Hp, $"tick {tick}, cell {x}");
                Assert.AreEqual(2, f.Streams.Data->Current.CellsHealed);
            }
        }
    }
}
