using Game.Core;
using Game.Core.Components;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Interaction;
using Game.Presentation.Components;
using Game.Presentation.Systems;
using Game.Simulation;
using Game.Simulation.Abilities;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>HUD-008's nine specified checks; direct presentation updates, no real-time waits.</summary>
    public unsafe sealed class HudSnapshotSystemTests
    {
        EcsTestWorld world;
        EntityManager em;
        SystemHandle system;
        FactionRelations relations;
        [SetUp] public void SetUp()
        {
            world = EcsTestWorld.Create(); em = world.EntityManager;
            world.AddSingleton(new GameplayActive());
            relations = FactionRelations.CreateDefault(Allocator.Persistent); world.AddSingleton(relations);
            system = world.World.GetOrCreateSystem<HudSnapshotSystem>();
        }
        [TearDown] public void TearDown() { world.Dispose(); relations.Dispose(); }
        Entity Player()
        {
            var e = Contact(0); em.AddComponent<PlayerShip>(e); return e;
        }
        Entity Contact(float x)
        {
            var e = em.CreateEntity(typeof(VoxelGrid), typeof(Allegiance), typeof(LocalToWorld));
            em.SetComponentData(e, new LocalToWorld { Value = float4x4.Translate(new float3(x, 0, 0)) });
            em.SetComponentData(e, new Allegiance { Faction = 0 }); return e;
        }
        HudSnapshot Tick(double seconds = 0, uint frame = 1)
        {
            ref var sut = ref world.World.Unmanaged.GetUnsafeSystemRef<HudSnapshotSystem>(system);
            sut.UseTestClock = true; sut.TestElapsed = seconds; sut.TestFrame = frame;
            system.Update(world.World.Unmanaged); em.CompleteAllTrackedJobs();
            return world.GetSingleton<HudSnapshot>();
        }
        DynamicBuffer<HudRadarBlip> Radar()
        {
            using var q = em.CreateEntityQuery(typeof(HudSnapshot)); return em.GetBuffer<HudRadarBlip>(q.GetSingletonEntity());
        }
        void Weapon(Entity owner, byte bits, float remaining, float cooldown, byte firing = 0)
        {
            var e = em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponState));
            em.SetComponentData(e, new FunctionalBlock { Grid = owner });
            em.SetComponentData(e, new WeaponState { Group = bits, CooldownRemainingTicks = remaining, CooldownTicks = cooldown, Firing = firing });
        }
        [Test] public void NoPlayer_FlagsNone()
        {
            Contact(10); var v = Tick(); Assert.AreEqual(HudFlags.None, v.Flags);
            Assert.AreEqual(0, v.FrameIndex); Assert.AreEqual(0, v.RadarVersion); Assert.AreEqual(0, Radar().Length);
            Player(); var duplicate = Player(); Assert.AreEqual(HudFlags.None, Tick().Flags);
            Assert.AreEqual(0, Radar().Length); em.DestroyEntity(duplicate); Assert.IsTrue((Tick().Flags & HudFlags.HasShip) != 0);
        }
        [Test] public void ShipValues_Copied()
        {
            var p = Player(); em.AddComponentData(p, new ShipStats { MassFraction = .75f });
            em.AddComponentData(p, new ShieldState { Hp = 30, MaxHp = 60, Active = 1 });
            em.AddComponentData(p, new ShipPower { Stored = 20, Capacity = 100 });
            em.AddComponentData(p, new PhysicsVelocity { Linear = new float3(3, 0, 4) });
            em.AddComponentData(p, new ShipCommand { Flags = ShipCommandFlags.Boost });
            var slots = em.AddBuffer<AbilitySlot>(p);
            slots.Add(new AbilitySlot { Ability = new ContentHash(17), CooldownTicks = 20, CooldownRemainingTicks = 5, Charges = 2, Active = 1 });
            slots.Add(new AbilitySlot { Ability = new ContentHash(18), Charges = 1 });
            world.AddSingleton(new CurrentStarSystemInfo { Id = new StarSystemId(7) });
            world.AddSingleton(new InteractionTarget { Target = Contact(30), DistanceM = 30, InRange = 1, GridPersistentId = 99 });
            var v = Tick(); Assert.AreEqual(.75f, v.Hull01); Assert.AreEqual(30, v.ShieldHp); Assert.AreEqual(60, v.ShieldMax);
            Assert.AreEqual(1, v.ShieldStatus); Assert.AreEqual(20, v.Energy); Assert.AreEqual(100, v.EnergyMax); Assert.AreEqual(5, v.SpeedMps);
            Assert.IsTrue((v.Flags & HudFlags.Boosting) != 0); Assert.AreEqual(2, v.Abilities.Length);
            Assert.AreEqual(.25f, v.Abilities[0].Cooldown01); Assert.AreEqual(0, v.Abilities[0].Ready); Assert.AreEqual(1, v.Abilities[1].Ready);
            Assert.AreEqual(7, v.StarSystemIdValue); Assert.AreEqual(99, v.InteractionGridPersistentId); Assert.AreEqual(30, v.InteractionDistanceM);
            Assert.IsTrue((v.Flags & HudFlags.HasInteraction) != 0);
        }
        [Test] public void WeaponGroups_Aggregated()
        {
            var p = Player(); Weapon(p, 1, 2, 4); Weapon(p, 2, 1, 4); Weapon(p, 4, 0, 4);
            Weapon(p, 3, 3, 4, 1); Weapon(p, 5, 1, 4); Weapon(p, 6, 2, 4); Weapon(p, 7, 0, 4);
            Weapon(Contact(20), 7, 4, 4);
            var groups = Tick().WeaponGroups; Assert.AreEqual(3, groups.Length);
            for (int i = 0; i < 3; i++) { Assert.AreEqual(i, groups[i].Group); Assert.AreEqual(4, groups[i].WeaponCount); }
            Assert.AreEqual(.25f, groups[0].Ready01); Assert.AreEqual(.25f, groups[1].Ready01); Assert.AreEqual(.5f, groups[2].Ready01);
            Assert.AreEqual(1, groups[0].Firing); Assert.AreEqual(1, groups[1].Firing); Assert.AreEqual(0, groups[2].Firing);
        }
        [Test] public void GroupBitsAbove2_Ignored()
        {
            var p = Player(); Weapon(p, 248, 10, 10, 1); var g = Tick().WeaponGroups;
            for (int i = 0; i < 3; i++) { Assert.AreEqual(0, g[i].WeaponCount); Assert.AreEqual(0, g[i].Firing); }
        }
        [Test] public void Target_RelationFromFactionRelations()
        {
            var p = Player(); var t = Contact(30); em.SetComponentData(t, new Allegiance { Faction = 1 });
            em.AddComponentData(p, new ShipTarget { Value = t }); em.AddComponentData(t, new ShipStats { MassFraction = .4f });
            em.AddComponentData(t, new ShieldState { Hp = 10, MaxHp = 40 }); em.AddComponentData(t, new GridSource { Blueprint = new ContentHash(123) });
            var v = Tick(); Assert.AreEqual(2, v.TargetRelation); Assert.AreEqual(30, v.TargetDistanceM);
            Assert.AreEqual(.4f, v.TargetHull01); Assert.AreEqual(.25f, v.TargetShield01); Assert.AreEqual(new ContentHash(123), v.TargetNameHash);
            em.SetComponentData(t, new Allegiance { Faction = 255 }); Assert.AreEqual(0, Tick(.1).TargetRelation);
        }
        [Test] public void Radar_Nearest64_Sorted()
        {
            Player(); for (int i = 100; i >= 1; i--) Contact(i * 10);
            Tick(); var radar = Radar(); Assert.AreEqual(64, radar.Length);
            for (int i = 0; i < 64; i++) Assert.AreEqual((i + 1) * 10, radar[i].OffsetXZ.x);
        }
        [Test] public void Radar_BeyondRange_Clamped()
        {
            Player(); Contact(3000);
            var anchor = em.CreateEntity(typeof(Interactable), typeof(StarSystemBody), typeof(LocalToWorld));
            em.SetComponentData(anchor, new LocalToWorld { Value = float4x4.Translate(new float3(4000, 0, 0)) });
            Tick(); Assert.AreEqual(2, Radar().Length); Assert.AreEqual(3, Radar()[1].Kind);
            var b = Radar()[0]; Assert.AreEqual(2000, math.length(b.OffsetXZ), .001f); Assert.AreEqual(2, b.Flags & 2);
        }
        [Test] public void Radar_RefreshRate10Hz()
        {
            Player(); uint initial = Tick().RadarVersion; uint final = initial;
            for (uint frame = 1; frame <= 144; frame++) final = Tick(frame / 144.0, frame).RadarVersion;
            Assert.That(final - initial, Is.InRange(9u, 11u));
        }
        [Test] public void Hulk_FlagSet()
        {
            var p = Player(); em.AddComponentData(p, new ShipStats { MassFraction = .8f });
            em.AddComponentData(p, new ShipPower { Stored = 50, Capacity = 100 });
            em.AddComponentData(p, new ShieldState { Hp = 20, MaxHp = 40, Active = 1 }); Tick();
            em.AddComponent<Hulk>(p); em.SetComponentData(p, default(ShipPower)); em.SetComponentData(p, default(ShieldState));
            em.SetComponentData(p, new ShipStats { MassFraction = .1f });
            var v = Tick(.1); Assert.IsTrue((v.Flags & HudFlags.IsHulk) != 0);
            Assert.AreEqual(.8f, v.Hull01); Assert.AreEqual(50, v.Energy); Assert.AreEqual(100, v.EnergyMax);
            Assert.AreEqual(20, v.ShieldHp); Assert.AreEqual(40, v.ShieldMax); Assert.AreEqual(1, v.ShieldStatus);
        }
    }
}
