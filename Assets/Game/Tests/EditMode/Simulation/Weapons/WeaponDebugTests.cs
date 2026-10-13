using System;
using System.Collections.Generic;
using Game.Simulation.Commands;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>SPEC-21 WPN-015 required snapshot fixture also checks the guarded statistics hook and safe incomplete worlds.</summary>
    public sealed class WeaponDebugTests
    {
        struct PublishStats : IJob
        {
            public NativeReference<ProjectileStoreStats> Stats;
            public ProjectileStoreStats Value;
            public void Execute() => Stats.Value = Value;
        }

        [Test] public void Snapshot_ListsWeapons()
        {
            using var fixture = EcsTestWorld.Create(); var em = fixture.EntityManager;
            var player = em.CreateEntity(typeof(PlayerShip)); em.AddBuffer<BlockInstance>(player);
            var other = em.CreateEntity(); em.AddBuffer<BlockInstance>(other);
            // Entity creation order differs from instance order; snapshots follow the canonical buffer.
            var second = Weapon(player, 2, 3, 2, -.5f, 0, 0, 1);
            var first = Weapon(player, 0, 7, 1, .25f, 12.5f, 1, 0);
            var unarmed = Weapon(player, 4, 0, 4, 1, -.3f, 0, 0);
            em.AddComponent<Disabled>(second); // Diagnostics still include disabled/inactive mounts.
            var foreign = Weapon(other, 0, 9, 1, 0, 0, 0, 0);
            var stale = Weapon(player, 5, 2, 1, 0, 0, 0, 0); em.DestroyEntity(stale);
            var incomplete = em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount));
            em.SetComponentData(incomplete, new FunctionalBlock { Grid = player, InstanceIndex = 6 });
            var dead = Weapon(player, 7, 4, 1, 0, 0, 0, 0);
            var reparented = Weapon(other, 8, 5, 1, 0, 0, 0, 0);
            var tombstone = Weapon(player, 9, 6, 1, 0, 0, 0, 0);
            var instances = em.GetBuffer<BlockInstance>(player);
            instances.ResizeUninitialized(10);
            for (int i = 0; i < instances.Length; i++) instances[i] = new BlockInstance { Block = 1, CellsAlive = 1, FunctionHp = 100 };
            Slot(0, first); Slot(1, foreign); Slot(2, second); Slot(3, first); Slot(4, unarmed); Slot(5, stale); Slot(6, incomplete);
            Slot(7, dead, BlockInstanceFlags.Destroyed); Slot(8, reparented);
            instances[9] = new BlockInstance { Block = 0, Functional = tombstone };
            var rows = new List<WeaponDebugRow> { default };
            WeaponDebug.Snapshot(em, player, rows); Assert.AreEqual(3, rows.Count);
            Check(rows[0], first, 7, 1, .25f, 12.5f, 1, 0);
            Check(rows[1], second, 3, 2, -.5f, 0, 0, 1);
            Check(rows[2], unarmed, 0, 4, 1, -.3f, 0, 0);
            var state = em.GetComponentData<WeaponState>(first); state.CooldownRemainingTicks = 4.5f; state.Firing = 0; em.SetComponentData(first, state);
            em.SetComponentData(first, new WeaponAim { Yaw = -.75f }); em.SetComponentData(first, new BeamState { Active = 1 });
            WeaponDebug.Snapshot(em, player, rows); Assert.AreEqual(3, rows.Count); Check(rows[0], first, 7, 1, -.75f, 4.5f, 0, 1);
            // Inspecting is observational: no native state or instance/weapon components are changed by either hook.
            Assert.AreEqual(first, em.GetBuffer<BlockInstance>(player)[0].Functional); Assert.AreEqual(4.5f, em.GetComponentData<WeaponState>(first).CooldownRemainingTicks);
            WeaponDebug.Snapshot(em, Entity.Null, rows); Assert.IsEmpty(rows);
            WeaponDebug.Snapshot(em, stale, rows); Assert.IsEmpty(rows);
            WeaponDebug.Snapshot(em, incomplete, rows); Assert.IsEmpty(rows);
            Assert.Throws<ArgumentNullException>(() => WeaponDebug.Snapshot(em, player, null));

            Assert.IsFalse(WeaponDebug.TryGetProjectileStats(em, out var absent)); Assert.AreEqual(0, absent.Live);
            var store = ProjectileStore.Create(4);
            try
            {
                var owner = em.CreateEntity(typeof(ProjectileStore)); em.SetComponentData(owner, store);
                var expected = new ProjectileStoreStats { Live = 2, PeakLive = 7, Fired = 19, DroppedFull = 3, Hits = 5, Expired = 4 };
                var producer = new PublishStats { Stats = store.Stats, Value = expected }.Schedule(store.Acquire(default));
                store.Release(producer); // Copied native memory writer is deliberately not an ECS component dependency.
                Assert.IsTrue(WeaponDebug.TryGetProjectileStats(em, out var actual)); Assert.IsTrue(producer.IsCompleted);
                Assert.AreEqual(expected.Live, actual.Live); Assert.AreEqual(expected.PeakLive, actual.PeakLive); Assert.AreEqual(expected.Fired, actual.Fired);
                Assert.AreEqual(expected.DroppedFull, actual.DroppedFull); Assert.AreEqual(expected.Hits, actual.Hits); Assert.AreEqual(expected.Expired, actual.Expired);
                Assert.AreEqual(0, store.Rows.Length, "Stats access must not repair or recompute authoritative counters.");
                var duplicate = em.CreateEntity(typeof(ProjectileStore)); Assert.IsFalse(WeaponDebug.TryGetProjectileStats(em, out actual)); Assert.AreEqual(0, actual.Live);
                em.DestroyEntity(duplicate); em.SetComponentData(owner, default(ProjectileStore)); Assert.IsFalse(WeaponDebug.TryGetProjectileStats(em, out actual));
                Assert.AreEqual(expected.Fired, store.Stats.Value.Fired); em.DestroyEntity(owner);
            }
            finally { store.Dispose(); }

            Entity Weapon(Entity root, int index, ushort weaponIndex, byte group, float yaw, float cooldown, byte firing, byte beam)
            {
                var entity = em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponState), typeof(WeaponAim), typeof(BeamState));
                em.SetComponentData(entity, new FunctionalBlock { Grid = root, InstanceIndex = index });
                em.SetComponentData(entity, new WeaponMount { WeaponIndex = weaponIndex });
                em.SetComponentData(entity, new WeaponState { Group = group, CooldownRemainingTicks = cooldown, Firing = firing });
                em.SetComponentData(entity, new WeaponAim { Yaw = yaw }); em.SetComponentData(entity, new BeamState { Active = beam });
                return entity;
            }
            void Slot(int index, Entity weapon, BlockInstanceFlags flags = default)
            {
                var buffer = em.GetBuffer<BlockInstance>(player); var instance = buffer[index]; instance.Functional = weapon; instance.Flags = flags; buffer[index] = instance;
            }
        }

        static void Check(WeaponDebugRow row, Entity weapon, ushort index, byte group, float yaw, float cooldown, byte firing, byte beam)
        {
            Assert.AreEqual(weapon, row.Weapon); Assert.AreEqual(index, row.WeaponIndex); Assert.AreEqual(group, row.Group);
            Assert.AreEqual(yaw, row.Yaw); Assert.AreEqual(cooldown, row.CooldownRemainingTicks); Assert.AreEqual(firing, row.Firing); Assert.AreEqual(beam, row.BeamActive);
        }
    }
}
