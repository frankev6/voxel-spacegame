using System.Collections;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Data.Components;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>FND-020: real boot/input/projectiles/current cells/split bodies and repeatable scene lifetime.</summary>
    public sealed class FiringRangeTests : ProductionSessionFixture
    {
        Entity target;
        IEnumerator StartRange()
        {
            yield return Run(Game.Session.NewGameAsync(new NewGameArgs("Range", 7, false, ContentId.None)));
            yield return Until(() => Game.Input.CurrentContext == InputContext.Flight && Game.Camera.HasTarget, "Flight camera ready");
            target = Services.Get<FiringRangeService>().Spawn(Root);
            yield return Until(() => EM.HasComponent<GridColliderState>(target) && EM.GetComponentData<GridColliderState>(target).Phase == GridColliderPhase.Ready,
                "Station has its real compound collider", 30);
            Assert.IsFalse(EM.HasComponent<PhysicsVelocity>(target), "The initial station is static");
        }
        void AimAt(float3 local)
        {
            float3 world = EM.GetComponentData<LocalTransform>(target).TransformPoint(local);
            Vector3 point = Game.Camera.Camera.WorldToScreenPoint(world);
            Host.Input.SetMouse(point.x, point.y, "none", false, 0, 0, 0);
        }
        T Singleton<T>() where T : unmanaged, IComponentData
        { using var query = EM.CreateEntityQuery(typeof(T)); return query.GetSingleton<T>(); }
        int Cells(Entity root)
        {
            int total = 0; foreach (var slot in EM.GetBuffer<BrickSlot>(root)) if (slot.Brick != Entity.Null) total += EM.GetComponentData<Brick>(slot.Brick).SolidCount;
            return total;
        }
        Cell CellAt(Entity root, int3 cell)
        {
            var grid = EM.GetComponentData<VoxelGrid>(root); int slot = BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims);
            if (slot < 0) return default;
            var brick = EM.GetBuffer<BrickSlot>(root)[slot].Brick;
            return brick == Entity.Null ? default : BrickWrite.Get(EM.GetBuffer<BrickCells>(brick), EM.GetBuffer<BrickSparseCells>(brick), BrickMath.LocalIndex(BrickMath.LocalOf(cell)));
        }
        [UnityTest] public IEnumerator RealFire_DamagesStationaryTarget()
        {
            yield return StartRange(); var pose = EM.GetComponentData<LocalTransform>(target); int cells = Cells(target);
            AimAt(new float3(0, 2, -5.9f)); Host.Input.SetKeys("Space", 0, false);
            yield return Until(() => EM.GetComponentData<GridDamageStats>(target).CellsDestroyed > 0, "Space must fire real installed guns and destroy target cells", 12);
            Host.Input.SetKeys("Space", 0, true);
            var store = Singleton<ProjectileStore>(); store.CompleteAccess(); Assert.Greater(store.Stats.Value.Fired, 0); Assert.Greater(store.Stats.Value.Hits, 0);
            Assert.Less(Cells(target), cells); Assert.AreEqual(pose.Position, EM.GetComponentData<LocalTransform>(target).Position);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                yield return Until(() => Game.Fx.Stats.EventsDrained > 0, "Real firing reaches the session effect rig");
                var muzzle = Game.Fx.transform.Find("Fx_fx_muzzle_default").GetComponent<UnityEngine.VFX.VisualEffect>();
                var kinetic = Game.Fx.transform.Find("Fx_fx_hit_kinetic_default").GetComponent<UnityEngine.VFX.VisualEffect>();
                var energy = Game.Fx.transform.Find("Fx_fx_hit_energy_default").GetComponent<UnityEngine.VFX.VisualEffect>();
                yield return Until(() => muzzle.GetInt("FxEventCount") > 0 &&
                    kinetic.GetInt("FxEventCount") + energy.GetInt("FxEventCount") > 0, "Real muzzle and hull-hit events reach their graphs");
            }
            using (Game.Pause.Acquire("Firing range pause"))
            {
                yield return Until(() => Game.Input.CurrentContext == InputContext.None, "Pause neutralizes fire");
                store.CompleteAccess(); int fired = store.Stats.Value.Fired;
                Host.Input.SetKeys("Space", 0, false); yield return new WaitForSecondsRealtime(.1f);
                store.CompleteAccess(); Assert.AreEqual(fired, store.Stats.Value.Fired);
                Host.Input.SetKeys("Space", 0, true);
            }
        }
        void ShootAlongBridge(uint id) => ShootFrom(id, new float3(-12.5f, .5f, -12));
        void ShootFrom(uint id, float3 localStart)
        {
            // A controlled projectile isolates hit testing from weapon spread; all movement/hits/damage still use production systems.
            var store = Singleton<ProjectileStore>(); store.CompleteAccess();
            var weapon = Singleton<WeaponDefsRef>().Value.Value.Weapons[1];
            float3 start = EM.GetComponentData<LocalTransform>(target).TransformPoint(localStart);
            store.Rows.Add(new ProjectileRow { Position = start, Velocity = new float3(0, 0, 400), Source = Root, Instigator = Root,
                ShotId = id, Weapon = 1, Type = weapon.Type, DamageMultiplier = 1, DeathTick = Singleton<SimClock>().Tick + 120 });
        }
        [UnityTest] public IEnumerator Shots_PassThroughHole()
        {
            yield return StartRange(); Assert.IsFalse(CellAt(target, new int3(-13, 0, 0)).IsAir);
            ShootAlongBridge(699); ShootAlongBridge(700);
            yield return Until(() => CellAt(target, new int3(-13, 0, 0)).IsAir, "First projectile opens a hole", 5);
            var store = Singleton<ProjectileStore>(); store.CompleteAccess(); int hits = store.Stats.Value.Hits;
            ShootAlongBridge(701);
            bool passed = false;
            yield return Until(() =>
            {
                store.CompleteAccess();
                foreach (var row in store.Rows) if (row.ShotId == 701 && row.Position.z > EM.GetComponentData<LocalTransform>(target).Position.z + 4) passed = true;
                return passed;
            }, "Second projectile passes through the removed cell instead of hitting the old collider", 5);
            store.CompleteAccess(); Assert.AreEqual(hits, store.Stats.Value.Hits);
            // Open both front-wall layers, then prove the next shot reaches the solid rear wall.
            var frontA = new int3(0, 2, -6); var frontB = new int3(0, 2, -5); var rear = new int3(0, 2, 5);
            byte rearHp = CellAt(target, rear).Hp;
            for (uint id = 710; id < 718 && (!CellAt(target, frontA).IsAir || !CellAt(target, frontB).IsAir); id++)
            {
                store.CompleteAccess(); int before = store.Stats.Value.Hits;
                ShootFrom(id, new float3(.5f, 2.5f, -12));
                yield return Until(() => { store.CompleteAccess(); return store.Stats.Value.Hits > before; }, "Front-wall impact", 5);
                yield return new WaitForSecondsRealtime(.04f);
            }
            Assert.IsTrue(CellAt(target, frontA).IsAir && CellAt(target, frontB).IsAir);
            ShootFrom(720, new float3(.5f, 2.5f, -12));
            yield return Until(() => CellAt(target, rear).IsAir || CellAt(target, rear).Hp < rearHp,
                "Projectile passes through the front hole and damages solid cells behind it", 5);
        }
        [UnityTest] public IEnumerator SeveredSection_BecomesMovingBody()
        {
            yield return StartRange(); AimAt(new float3(-11.5f, .5f, .5f)); Host.Input.SetKeys("Space", 0, false);
            Entity piece = Entity.Null;
            yield return Until(() =>
            {
                using var query = EM.CreateEntityQuery(typeof(HulkInfo), typeof(GridMass));
                using var roots = query.ToEntityArray(Allocator.Temp);
                foreach (var root in roots) if (EM.GetComponentData<HulkInfo>(root).Cause == HulkCause.Split && EM.GetComponentData<GridMass>(root).Data.CellCount > 50) { piece = root; return true; }
                return false;
            }, "Severing the single bridge must release its pod", 12);
            Host.Input.SetKeys("Space", 0, true);
            yield return Until(() => EM.GetComponentData<GridDamageStats>(target).CellsDetached > 50, "Parent cells are removed on the following tick");
            Assert.IsTrue(EM.HasComponent<PhysicsCollider>(piece)); Assert.IsTrue(EM.HasComponent<SplitGrace>(piece));
            var start = EM.GetComponentData<LocalTransform>(piece).Position;
            yield return Until(() => math.distance(start, EM.GetComponentData<LocalTransform>(piece).Position) > .2f, "Detached physical pod must move independently");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                yield return Until(() =>
                {
                    foreach (var slot in EM.GetBuffer<BrickSlot>(piece))
                        if (slot.Brick != Entity.Null && (!EM.HasComponent<BrickRenderState>(slot.Brick) || EM.GetComponentData<BrickRenderState>(slot.Brick).HasMesh == 0)) return false;
                    return true;
                }, "Every detached brick must have an applied render mesh", 10);
            var instances = EM.GetBuffer<BlockInstance>(piece); bool functional = false;
            foreach (var instance in instances) if (instance.Functional != Entity.Null)
            {
                functional = true; Assert.AreEqual(piece, EM.GetComponentData<FunctionalBlock>(instance.Functional).Grid);
                foreach (var member in EM.GetBuffer<LinkedEntityGroup>(target)) Assert.AreNotEqual(instance.Functional, member.Value);
            }
            Assert.IsTrue(functional, "Pod carries the installed thruster with it");
        }
        [UnityTest] public IEnumerator Restart_ClearsCombatState()
        {
            yield return StartRange(); int initial = Cells(target); Entity old = target;
            ShootAlongBridge(799); ShootAlongBridge(800); yield return Until(() => EM.GetComponentData<GridDamageStats>(target).CellsDestroyed > 0, "Combat happened before restart");
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
            var store = Singleton<ProjectileStore>(); store.CompleteAccess(); Assert.AreEqual(0, store.Rows.Length); Assert.IsFalse(EM.Exists(old));
            using (var query = EM.CreateEntityQuery(typeof(Hulk))) Assert.AreEqual(0, query.CalculateEntityCount());
            yield return StartRange(); Assert.AreEqual(initial, Cells(target)); Assert.AreEqual(0, EM.GetComponentData<GridDamageStats>(target).CellsDestroyed);
            store = Singleton<ProjectileStore>(); store.CompleteAccess(); Assert.AreEqual(0, store.Rows.Length);
        }
    }
}
