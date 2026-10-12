using System;
using Game.Core.Components;
using Game.Data.Combat;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public unsafe sealed class DamageResolveSystemTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory Grid = new TestGridFactory();
            public readonly SystemHandle Owner, Resolver;
            public readonly Entity Clock;
            public readonly DamageStreams Streams;
            public Fixture()
            {
                Owner = Grid.World.CreateSystem<DamageStreamsSystem>(); Resolver = Grid.World.CreateSystem<DamageResolveSystem>();
                Clock = Grid.EM.CreateSingleton(new SimClock());
                using var q = Grid.EM.CreateEntityQuery(typeof(DamageStreams)); Streams = q.GetSingleton<DamageStreams>();
            }
            public Entity Box(int cells = 4) => Grid.Spawn(Grid.Box(0, new int3(cells - 1, 0, 0)));
            public void Begin(uint tick)
            { Grid.EM.SetComponentData(Clock, new SimClock { Tick = tick }); Owner.Update(Grid.World.Unmanaged); }
            public void Resolve()
            { Resolver.Update(Grid.World.Unmanaged); Grid.EM.CompleteAllTrackedJobs(); Streams.Data->ResolverHandle.Complete(); }
            public void Post(DamageEvent e) => Streams.Damage.PostFromMainThread(e);
            public Cell Cell(Entity root, int x)
            { using var w = Grid.Writer(root); return w.Access.TryGetCell(new int3(x,0,0), out var cell, out _, out _) ? cell : default; }
            public void Dispose() => Grid.Dispose();
        }
        static DamageEvent Hit(Entity root, float amount = 40, int x = 0) => new DamageEvent
        { Grid = root, Amount = amount, LocalPoint = new float3(x * .5f + .25f, .25f, .25f), Producer = ProducerKind.Projectile };
        [Test] public void DamageImmune_NoCellChange()
        {
            using var f = new Fixture(); var root = f.Box(); f.Grid.EM.AddComponent<DamageImmune>(root);
            ulong before = CellStateHasher.ComputeWorldHash(f.Grid.EM); f.Begin(1);
            var e = Hit(root, 1000); e.WorldImpulse = new float3(500,0,0); f.Post(e); f.Resolve();
            Assert.AreEqual(before, CellStateHasher.ComputeWorldHash(f.Grid.EM)); Assert.AreEqual(1, f.Streams.Data->Current.EventsDropped);
            Assert.AreEqual(default(PendingImpulse), f.Grid.EM.GetComponentData<PendingImpulse>(root)); Assert.AreEqual(0, f.Streams.Data->Removed.Length);
        }
        [Test] public void LastDamageSource_IsLastApplied()
        {
            using var f = new Fixture(); var root = f.Box(); var a = f.Grid.EM.CreateEntity(); var b = f.Grid.EM.CreateEntity(); var missed = f.Grid.EM.CreateEntity();
            f.Begin(17); var e = Hit(root, 10); e.Source = b; e.Instigator = a; f.Post(e); e.Source = a; f.Post(e);
            e.Source = missed; e.LocalPoint = new float3(100); f.Post(e); f.Resolve();
            var stats = f.Grid.EM.GetComponentData<GridDamageStats>(root);
            Assert.AreEqual(b, stats.LastDamageSource); Assert.AreEqual(a, stats.LastDamageInstigator); Assert.AreEqual(17, stats.LastDamageTick);
        }
        [Test] public void DetachPass_RunsBeforeEvents()
        {
            using var f = new Fixture(); var root = f.Box(); var onlyDetach = f.Box();
            var mask = new CellMask(); mask.Words[0] = 1;
            f.Streams.Data->PendingDetachNext.Add(new PendingDetach { Grid = root, Mask = mask, Reason = CellRemovedReason.DetachedBody });
            f.Streams.Data->PendingDetachNext.Add(new PendingDetach { Grid = onlyDetach, Mask = mask, Reason = CellRemovedReason.DetachedBody });
            f.Begin(1); var e = Hit(root, 100); e.LocalDir = new float3(1,0,0); e.Penetration = 1; f.Post(e); f.Resolve();
            Assert.IsTrue(f.Cell(root, 0).IsAir); Assert.IsTrue(f.Cell(root, 1).IsAir); Assert.IsTrue(f.Cell(onlyDetach, 0).IsAir);
            var removed = f.Streams.Data->Removed; Assert.AreEqual(3, removed.Length);
            Assert.AreEqual(CellRemovedReason.DetachedBody, removed[0].Reason); Assert.AreEqual(CellRemovedReason.Destroyed, removed[1].Reason);
            Assert.AreEqual(new int3(1,0,0), removed[1].Cell);
        }
        [Test] public void Revision_BumpsOncePerTick()
        {
            using var f = new Fixture(); var root = f.Box(); uint before = f.Grid.EM.GetComponentData<VoxelGrid>(root).Revision;
            f.Begin(1); f.Post(Hit(root, 40)); f.Post(Hit(root, 40, 1)); f.Resolve();
            Assert.AreEqual(before + 1, f.Grid.EM.GetComponentData<VoxelGrid>(root).Revision);
            Assert.Greater(f.Streams.Data->TouchedBricks.Length, 0);
            foreach (var touch in f.Streams.Data->TouchedBricks) Assert.IsTrue(f.Grid.EM.IsComponentEnabled<BrickDirty>(touch.Brick));
            f.Begin(2); f.Resolve(); Assert.AreEqual(before + 1, f.Grid.EM.GetComponentData<VoxelGrid>(root).Revision);
            Assert.AreEqual(0, f.Streams.Data->Removed.Length); Assert.AreEqual(0, f.Streams.Data->GridResults.Length);
        }
        [Test] public void DroppedEvents_AreCounted()
        {
            using var f = new Fixture(); var root = f.Box(); var stale = f.Grid.EM.CreateEntity(); f.Grid.EM.DestroyEntity(stale);
            f.Begin(1); f.Post(Hit(Entity.Null)); f.Post(Hit(stale)); f.Post(Hit(f.Grid.EM.CreateEntity())); f.Post(Hit(root, 0)); f.Post(Hit(root, -1));
            LogAssert.Expect(LogType.Warning, "[Damage] bad-event: non-finite field; dropped.");
            foreach (int field in new[] {0,1,2,3,4,5})
            {
                var e = Hit(root); if (field == 0) e.Amount = float.NaN; if (field == 1) e.Radius = float.PositiveInfinity;
                if (field == 2) e.Penetration = float.NaN; if (field == 3) e.LocalPoint.x = float.NaN;
                if (field == 4) e.LocalDir.z = float.NaN; if (field == 5) e.WorldImpulse.y = float.NegativeInfinity; f.Post(e);
            }
            f.Resolve(); Assert.AreEqual(11, f.Streams.Data->Current.EventsIn); Assert.AreEqual(11, f.Streams.Data->Current.EventsDropped);
            Assert.AreEqual(255, f.Cell(root, 0).Hp);
        }
        [Test] public void PendingImpulse_AccumulatesWithTick()
        {
            using var f = new Fixture(); var root = f.Box(); f.Begin(8);
            var e = Hit(root, 1); e.WorldImpulse = new float3(2,0,0); f.Post(e); e.WorldImpulse = new float3(0,0,3); f.Post(e);
            e.Flags = DamageEventFlags.NoImpulse; e.WorldImpulse = new float3(100); f.Post(e); f.Resolve();
            var impulse = f.Grid.EM.GetComponentData<PendingImpulse>(root);
            Assert.AreEqual(new float3(2,0,3), impulse.LinearWorld); Assert.AreEqual(math.cross(new float3(.25f), impulse.LinearWorld), impulse.AngularWorld); Assert.AreEqual(8, impulse.Tick);
            f.Begin(9); e.Flags = 0; e.WorldImpulse = new float3(1,0,0); f.Post(e); f.Resolve();
            impulse = f.Grid.EM.GetComponentData<PendingImpulse>(root); Assert.AreEqual(new float3(1,0,0), impulse.LinearWorld); Assert.AreEqual(9, impulse.Tick);
        }
        [Test] public void Results_IndependentOfStreamOrderAndWorkers()
        {
            int prior = JobsUtility.JobWorkerCount; bool safety = BurstCompiler.Options.EnableBurstSafetyChecks;
            var leak = NativeLeakDetection.Mode; ulong? baseline = null; CellRemoved[] expected = null;
            try
            {
                BurstCompiler.Options.EnableBurstSafetyChecks = true; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
                foreach (int workers in new[] {1, math.max(1, prior)}) for (int order = 0; order < 3; order++)
                {
                    JobsUtility.JobWorkerCount = workers;
                    using var f = new Fixture(); var roots = new Entity[10]; for (int g = 0; g < 10; g++) roots[g] = f.Box(256);
                    var sources = new[] {f.Grid.EM.CreateEntity(), f.Grid.EM.CreateEntity(), f.Grid.EM.CreateEntity()}; f.Begin(3);
                    ref var state = ref f.Grid.World.Unmanaged.ResolveSystemStateRef(f.Owner);
                    for (int registration = 0; registration < 3; registration++)
                    {
                        int group = (registration + order) % 3;
                        var writer = f.Streams.Damage.BeginWrite(ref state, ProducerKind.Projectile, 2);
                        for (int index = 0; index < 2; index++)
                        {
                            writer.BeginForEachIndex(index);
                            for (int n = group; n < 2000; n += 3)
                            {
                                if (n % 2 != index) continue;
                                var e = Hit(roots[n % 10], n % 4 == 0 ? 120 : 37, n / 10);
                                e.Source = sources[group]; e.Sequence = (uint)n; writer.Write(e);
                            }
                            writer.EndForEachIndex();
                        }
                        f.Streams.Damage.EndWrite(default);
                    }
                    f.Resolve(); ulong hash = CellStateHasher.ComputeWorldHash(f.Grid.EM); var actual = f.Streams.Data->Removed.AsArray().ToArray();
                    Assert.AreEqual(2000, f.Streams.Data->Current.EventsIn); Assert.AreEqual(10, f.Streams.Data->Current.GridsResolved); Assert.Greater(actual.Length, 0);
                    // Recreated Worlds reuse indices with new Entity versions. Validate the current
                    // handle, then compare grid identity by PersistentId; every other field stays exact.
                    for (int i = 0; i < actual.Length; i++)
                    {
                        Assert.Contains(actual[i].Grid, roots); Assert.IsTrue(f.Grid.EM.Exists(actual[i].Grid));
                        var cell = actual[i]; cell.Grid = new Entity { Index = (int)f.Grid.EM.GetComponentData<PersistentId>(cell.Grid).Value, Version = 1 }; actual[i] = cell;
                    }
                    if (!baseline.HasValue) { baseline = hash; expected = actual; }
                    else
                    {
                        Assert.AreEqual(baseline.Value, hash); Assert.AreEqual(expected.Length, actual.Length);
                        for (int i = 0; i < actual.Length; i++)
                        {
                            var a = expected[i]; var b = actual[i];
                            Assert.AreEqual(a.Grid, b.Grid, $"grid {i}"); Assert.AreEqual(a.Cell, b.Cell, $"cell {i}");
                            Assert.AreEqual(a.Block, b.Block, $"block {i}"); Assert.AreEqual(a.Color, b.Color, $"color {i}");
                            Assert.AreEqual(a.WorldPos, b.WorldPos, $"position {i}"); Assert.AreEqual(a.WorldVel, b.WorldVel, $"velocity {i}");
                            Assert.AreEqual(a.Kick, b.Kick, $"kick {i}"); Assert.AreEqual(a.Reason, b.Reason, $"reason {i}");
                            Assert.AreEqual(a.Orient, b.Orient, $"orientation {i}"); Assert.AreEqual(a.InstanceIndex, b.InstanceIndex, $"instance {i}");
                        }
                    }
                }
                TestContext.WriteLine($"2000 events, 10 grids, 3 stream orders, workers 1/{prior}: hash={baseline:X16}, removed={expected.Length}");
            }
            finally { JobsUtility.JobWorkerCount = prior; BurstCompiler.Options.EnableBurstSafetyChecks = safety; NativeLeakDetection.Mode = leak; }
        }
    }
}
