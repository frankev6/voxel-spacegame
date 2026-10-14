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
            // QA-001: campaigns no longer auto-spawn the test station (config_firing_range); the range command spawns it.
            Assert.AreEqual(Entity.Null, Services.Get<FiringRangeService>().Target, "Campaign entry stays free of the test station");
            target = Services.Get<FiringRangeService>().Spawn(Root);
            Assert.AreNotEqual(Entity.Null, target, "The range command spawns the station");
            yield return Until(() => EM.HasComponent<GridColliderState>(target) && EM.GetComponentData<GridColliderState>(target).Phase == GridColliderPhase.Ready,
                "Station has its real compound collider", 30);
            Assert.IsFalse(EM.HasComponent<PhysicsVelocity>(target), "The initial station is static");
        }
        void AimAt(float3 local)
        {
            float3 world = EM.GetComponentData<LocalTransform>(target).TransformPoint(local * 3);
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
            Assert.AreEqual(global::Game.Simulation.Allegiance.Player, EM.GetComponentData<global::Game.Simulation.Allegiance>(Root),
                "Normal player spawning must supply faction identity before hostile AI targeting");
            Assert.AreEqual(target, Services.Get<FiringRangeService>().Spawn(Root), "Range command reuses the spawned target");
            Assert.AreEqual(-2f, pose.Position.y, "Station floor intersects the firing plane");
            var bounds = EM.GetComponentData<VoxelGrid>(target);
            Assert.GreaterOrEqual(bounds.BrickDims.x * BrickMath.Size, 153, "Production station spans three times its original 51-cell width");
            Assert.Greater(cells, 10000, "Geometry is enlarged at unchanged voxel resolution");
            AimAt(new float3(0, .5f, 2)); Host.Input.SetKeys("Space", 0, true);
            // Let the injected devices and aim reach the real sampler before pressing the button.
            yield return Until(() => EM.GetComponentData<Game.Simulation.Commands.ShipCommand>(Root).AimWorld.z > pose.Position.z, "Injected aim reached the flight sampler");
            Host.Input.SetKeys("Space", 0, false);
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (EM.GetComponentData<GridDamageStats>(target).CellsDestroyed == 0 && Time.realtimeSinceStartupAsDouble < deadline)
            { yield return null; }
            var diagnostic = Singleton<ProjectileStore>(); diagnostic.CompleteAccess();
            var command = EM.GetComponentData<Game.Simulation.Commands.ShipCommand>(Root);
            TestContext.WriteLine($"Range fire: fired={diagnostic.Stats.Value.Fired}, hits={diagnostic.Stats.Value.Hits}, command={command.AimWorld}, valid={command.AimValid}, fire={command.FireGroups}, camera={Game.Camera.Camera.pixelRect}, target={pose.Position}");
            for (int i = 0; i < math.min(4,diagnostic.Rows.Length); i++) TestContext.WriteLine($"Shot {diagnostic.Rows[i].Position} velocity {diagnostic.Rows[i].Velocity}");
            TestContext.WriteLine($"Input: context={Game.Input.CurrentContext}, pressed={Game.Input.Controls.Flight.FirePrimary.IsPressed()}, enabled={Game.Input.Controls.Flight.FirePrimary.enabled}, keyboard={UnityEngine.InputSystem.Keyboard.current?.spaceKey.isPressed}");
            foreach (var device in UnityEngine.InputSystem.InputSystem.devices) TestContext.WriteLine($"Device {device.name} enabled={device.enabled}");
            Assert.Greater(EM.GetComponentData<GridDamageStats>(target).CellsDestroyed,0,"Space must destroy target cells");
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
            float3 start = EM.GetComponentData<LocalTransform>(target).TransformPoint(localStart * 3);
            store.Rows.Add(new ProjectileRow { Position = start, Velocity = new float3(0, 0, 400), Source = Root, Instigator = Root,
                ShotId = id, Weapon = 1, Type = weapon.Type, DamageMultiplier = 1, DeathTick = Singleton<SimClock>().Tick + 120 });
        }
        [UnityTest] public IEnumerator Shots_PassThroughHole()
        {
            yield return StartRange(); Assert.IsFalse(CellAt(target, new int3(-38, 1, 0)).IsAir);
            for (int z = 0; z < 3; z++)
            {
                ShootAlongBridge((uint)(680 + z * 2)); ShootAlongBridge((uint)(681 + z * 2));
                int depth = z; yield return Until(() => CellAt(target,new int3(-38,1,depth)).IsAir,"Open enlarged bridge tunnel layer",5);
            }
            yield return Until(() => CellAt(target, new int3(-38, 1, 0)).IsAir, "First projectile opens a hole", 5);
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
            var frontA = new int3(1, 7, -18); var frontB = new int3(1, 7, -13); var rear = new int3(1, 7, 15);
            byte rearHp = CellAt(target, rear).Hp;
            for (uint id = 710; id < 734 && (!CellAt(target, frontA).IsAir || !CellAt(target, frontB).IsAir); id++)
            {
                store.CompleteAccess(); int before = store.Stats.Value.Hits;
                ShootFrom(id, new float3(.5f, 2.5f, -12));
                yield return Until(() => { store.CompleteAccess(); return store.Stats.Value.Hits > before; }, "Front-wall impact", 5);
                yield return new WaitForSecondsRealtime(.04f);
            }
            Assert.IsTrue(CellAt(target, frontA).IsAir && CellAt(target, frontB).IsAir);
            ShootFrom(740, new float3(.5f, 2.5f, -12));
            yield return Until(() => CellAt(target, rear).IsAir || CellAt(target, rear).Hp < rearHp,
                "Projectile passes through the front hole and damages solid cells behind it", 5);
        }
        [UnityTest] public IEnumerator SeveredSection_BecomesMovingBody()
        {
            yield return StartRange();
            // Clear the enlarged bridge cross-section using controlled real projectile rows.
            for (int z = 0; z < 3; z++) for (int y = 0; y < 3; y++)
            {
                ShootFrom((uint)(900+z*6+y*2),new float3(-11.5f,(y+.5f)/3,-12));
                ShootFrom((uint)(901+z*6+y*2),new float3(-11.5f,(y+.5f)/3,-12));
                int height = y, depth = z; yield return Until(() => CellAt(target,new int3(-35,height,depth)).IsAir,"Cut enlarged bridge cross-section",5);
            }
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
