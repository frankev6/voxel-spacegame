using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>The five SPEC-21 WPN-006 scenarios, through the scheduled production aim system.</summary>
    public sealed class WeaponAimTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly EcsTestWorld World;
            public readonly Entity Root, Mount, Target;
            readonly BlobAssetReference<WeaponDefsBlob> weapons;
            public EntityManager Em => World.EntityManager;
            public WeaponAim Aim => Em.GetComponentData<WeaponAim>(Mount);
            public Fixture(bool fixedWeapon = false)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var blob = ref builder.ConstructRoot<WeaponDefsBlob>();
                var rows = builder.Allocate(ref blob.Weapons, 2);
                rows[1] = new WeaponDef { Index = 1, Delivery = WeaponDelivery.Projectile,
                    IsFixed = (byte)(fixedWeapon ? 1 : 0), TraverseHalfArcRad = math.radians(45),
                    TraverseRateRad = math.radians(60), MaxElevationRad = math.radians(20),
                    FireConeRad = math.radians(3), ProjectileSpeed = 100, LifetimeMaxTicks = 600, Range = 1000 };
                weapons = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                World = EcsTestWorld.Create(typeof(WeaponAimSystem));
                World.AddSingleton(new WeaponDefsRef { Value = weapons });
                World.AddSingleton(new WeaponTuning { FixedConvergenceRad = math.radians(3) });
                World.AddGameplayActive();
                Root = Em.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(ShipCommand), typeof(WeaponControl), typeof(ShipTarget));
                Em.SetComponentData(Root, LocalTransform.Identity);
                Target = Em.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity));
                Em.SetComponentData(Target, LocalTransform.FromPosition(0, 0, 100));
                Mount = Em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponAim), typeof(WeaponState));
                Em.SetComponentData(Mount, new FunctionalBlock { Grid = Root });
                Em.SetComponentData(Mount, new WeaponMount { Initialized = 1, WeaponIndex = 1,
                    ForwardLocal = new float3(0, 0, 1), UpLocal = new float3(0, 1, 0) });
            }
            public void Point(float3 point) => Em.SetComponentData(Root, new ShipCommand { AimValid = 1, AimWorld = point });
            public void Step() { World.StepTicks(1); Em.CompleteAllTrackedJobs(); }
            public void Dispose() { World.Dispose(); weapons.Dispose(); }
        }
        [Test] public void Turret_ArcAndRate()
        {
            using var f = new Fixture(); f.Point(new float3(100, 0, 0));
            float previous = 0;
            for (int i = 0; i < 90; i++)
            {
                f.Step(); var aim = f.Aim;
                Assert.LessOrEqual(math.abs(aim.Yaw - previous), math.radians(60) * SimTime.FixedTimestep + 1e-6f);
                Assert.LessOrEqual(math.abs(aim.Yaw), math.radians(45) + 1e-6f);
                previous = aim.Yaw;
            }
            Assert.AreEqual(math.radians(45), f.Aim.Yaw, 1e-6f); Assert.AreEqual(0, f.Aim.Aligned);
            f.Point(new float3(0, 0, 100)); for (int i = 0; i < 60; i++) f.Step();
            Assert.AreEqual(0, f.Aim.Yaw, 1e-6f); Assert.AreEqual(1, f.Aim.Aligned);
        }
        [Test] public void Fixed_Convergence()
        {
            using var f = new Fixture(true);
            foreach (float angle in new[] { -60f, -2f, 2f, 60f })
            {
                float radians = math.radians(angle); f.Point(new float3(math.sin(radians), 0, math.cos(radians)) * 100); f.Step();
                Assert.AreEqual(math.clamp(radians, -math.radians(3), math.radians(3)), f.Aim.Yaw, 1e-6f);
                Assert.AreEqual(math.abs(angle) <= 6 ? 1 : 0, f.Aim.Aligned);
                Assert.AreEqual(1, f.Aim.HasAim);
            }
        }
        [Test] public void Lead_HitsMovingTarget()
        {
            using var f = new Fixture();
            float3 shooter = new float3(7, 0, 4), target = new float3(20, 0, 3), center = new float3(0, 0, 100);
            var rotation = quaternion.RotateY(math.radians(30));
            f.Em.AddComponentData(f.Target, new VoxelGrid { CellMin = 0, CellMax = new int3(19, 3, 7), CellSize = .5f });
            f.Em.SetComponentData(f.Target, LocalTransform.FromPositionRotation(center - math.rotate(rotation, new float3(5, 1, 2)), rotation));
            f.Em.SetComponentData(f.Root, new PhysicsVelocity { Linear = shooter });
            f.Em.SetComponentData(f.Target, new PhysicsVelocity { Linear = target });
            f.Em.SetComponentData(f.Root, new ShipTarget { Value = f.Target });
            f.Em.SetComponentData(f.Root, new WeaponControl { AimMode = WeaponAimMode.TargetLead });
            for (int i = 0; i < 60; i++) f.Step();
            var aim = f.Aim; Assert.AreEqual(1, aim.HasAim); Assert.AreEqual(1, aim.InRange); Assert.AreEqual(1, aim.Aligned);
            float3 relative = aim.FireDirLocal * 100 + shooter - target;
            float intercept = math.dot(center, relative) / math.lengthsq(relative);
            Assert.Greater(intercept, 0); Assert.LessOrEqual(intercept, 10);
            Assert.Less(math.distance((aim.FireDirLocal * 100 + shooter) * intercept, center + target * intercept), .1f);
        }
        [Test] public void Elevation_Clamped()
        {
            using var f = new Fixture();
            foreach (float sign in new[] { -1f, 1f })
            {
                f.Point(new float3(0, sign * 100, 1)); f.Step();
                Assert.AreEqual(sign * math.radians(20), math.asin(f.Aim.FireDirLocal.y), 1e-6f);
                Assert.AreEqual(1, math.length(f.Aim.FireDirLocal), 1e-6f);
            }
        }
        [Test] public void SkipsDormantHulkSpawning()
        {
            using var f = new Fixture(); f.Point(new float3(50, 0, 100)); f.Step();
            float yaw = f.Aim.Yaw;
            f.Em.AddComponent<Dormant>(f.Root); f.Step(); AssertStopped();
            f.Em.SetComponentEnabled<Dormant>(f.Root, false); f.Em.AddComponent<GridSpawning>(f.Root); f.Step(); AssertStopped();
            f.Em.SetComponentEnabled<GridSpawning>(f.Root, false); f.Em.AddComponent<Hulk>(f.Root); f.Step(); AssertStopped();
            void AssertStopped() { Assert.AreEqual(0, f.Aim.HasAim); Assert.AreEqual(yaw, f.Aim.Yaw); }
        }
    }
}
