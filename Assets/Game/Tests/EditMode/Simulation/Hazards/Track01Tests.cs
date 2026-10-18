using Game.Data.Definitions;
using Game.Galaxy.Racing;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Hazards
{
    /// <summary>RACE-015 (part): Track 01's set pieces as spawned by TrackSpawnService on the real track asset: the reactor pulses every 8 s
    /// and every 4 s once the leader starts lap 3, apart from the track's own zones.</summary>
    public sealed class Track01Tests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(Game.Voxel.Damage.DamageStreamsSystem), typeof(ZoneSystem), typeof(MechanismSystem) };
        const ushort Group = 18;

        Entity Reactor()
        {
            ushort setPiece = TrackSpawnService.SetPieceGroup(Group, 1);
            using var q = EM.CreateEntityQuery(typeof(WorldZone), typeof(ZoneSchedule));
            using var zones = q.ToEntityArray(Allocator.Temp);
            Entity found = Entity.Null; int n = 0;
            foreach (var z in zones) if (EM.GetComponentData<WorldZone>(z).Group == setPiece) { found = z; n++; }
            Assert.AreEqual(1, n, "one reactor in set piece 1");
            return found;
        }

        [Test] public void LapEvents_Fire()
        {
            var track = UnityEditor.AssetDatabase.LoadAssetAtPath<TrackDefinition>("Assets/Game/Content/Racing/track_01.asset");
            Assert.IsNotNull(track);
            TrackSpawnService.Spawn(EM, track, float2.zero, Group, null);
            Step(2);
            var reactor = Reactor();
            Assert.AreEqual(480u, EM.GetComponentData<ZoneSchedule>(reactor).PeriodTicks, "laps 1 and 2: every 8 s");
            using (var q = EM.CreateEntityQuery(typeof(RaceLapSignal)))
            {
                var e = q.GetSingletonEntity();
                EM.SetComponentData(e, new RaceLapSignal { LeaderLap = 2 }); Step(3);
                Assert.AreEqual(480u, EM.GetComponentData<ZoneSchedule>(reactor).PeriodTicks, "still every 8 s on lap 2");
                EM.SetComponentData(e, new RaceLapSignal { LeaderLap = 3 }); Step(3);
            }
            Assert.AreEqual(240u, EM.GetComponentData<ZoneSchedule>(reactor).PeriodTicks, "lap 3: every 4 s");
            // The track's own zones (walls, boosts, gates) keep no schedule.
            using (var q = EM.CreateEntityQuery(typeof(WorldZone), typeof(ZoneSchedule)))
                Assert.AreEqual(1, q.CalculateEntityCount(), "only the reactor is scheduled");
            TrackSpawnService.Despawn(EM, Group);
            using (var q = EM.CreateEntityQuery(typeof(WorldZone))) Assert.AreEqual(0, q.CalculateEntityCount(), "despawn clears the set pieces too");
            using (var q = EM.CreateEntityQuery(typeof(Mechanism))) Assert.AreEqual(0, q.CalculateEntityCount(), "and the track's mechanisms");
        }
        /// <summary>The portal, the rig and the flight-deck door with its switch: what each is and which set piece drives it.</summary>
        [Test] public void SetPieces_Spawn()
        {
            var track = UnityEditor.AssetDatabase.LoadAssetAtPath<TrackDefinition>("Assets/Game/Content/Racing/track_01.asset");
            Assert.IsNotNull(track);
            Entity Stand(ShipBlueprintDefinition b, float2 at, float yaw, ulong id)
            {
                var e = EM.CreateEntity(typeof(Unity.Transforms.LocalTransform));
                EM.SetComponentData(e, Unity.Transforms.LocalTransform.FromPositionRotation(new float3(at.x, 0, at.y), quaternion.RotateY(yaw)));
                EM.SetName(e, b.IdString);
                return e;
            }
            TrackSpawnService.Spawn(EM, track, float2.zero, Group, null, Stand, false, Stand);
            using (var q = EM.CreateEntityQuery(typeof(PortalLink), typeof(WorldZone)))
            {
                Assert.AreEqual(1, q.CalculateEntityCount(), "one lane-gate portal");
                var link = q.GetSingleton<PortalLink>(); var entry = q.GetSingleton<WorldZone>();
                Assert.AreEqual(6, link.JamSeconds, 1e-4f, "jams 6 s after each use");
                Assert.AreEqual(140, entry.HalfExtents.x * 2, 1e-3f, "across the whole route");
                foreach (var e in EM.GetBuffer<ZoneEffect>(q.GetSingletonEntity())) Assert.AreEqual(HazardSpawn.PortalGateId, e.Id, "never counted as a race gate");
            }
            Entity rig = Entity.Null, door = Entity.Null, post = Entity.Null;
            using (var q = EM.CreateEntityQuery(typeof(Mover)))
            using (var movers = q.ToEntityArray(Allocator.Temp))
                foreach (var m in movers) { if (EM.GetComponentData<Mover>(m).Path == MoverPath.Rotate) rig = m; else door = m; }
            using (var q = EM.CreateEntityQuery(typeof(MechanismGroup), typeof(Unity.Transforms.LocalTransform)))
                post = q.GetSingletonEntity();
            Assert.AreNotEqual(Entity.Null, rig, "the salvage rig turns"); Assert.AreNotEqual(Entity.Null, door, "the flight-deck door");
            var rigMover = EM.GetComponentData<Mover>(rig);
            Assert.AreEqual(1900, rigMover.Origin.x, 1e-3f); Assert.AreEqual(1700, rigMover.Origin.z, 1e-3f);
            Assert.AreEqual(2 * math.PI / 50, rigMover.Rate, 1e-5f, "one turn in 50 s");
            var doorMover = EM.GetComponentData<Mover>(door);
            Assert.AreEqual(MoverPath.Toggle, doorMover.Path); Assert.AreEqual(1, doorMover.Reverse, "rests open");
            var points = EM.GetBuffer<MoverPoint>(door);
            Assert.AreEqual(64, math.distance(points[0].Pose, points[1].Pose), 1e-2f, "slides 64 m into the hull");
            Assert.AreEqual(EM.GetComponentData<MechanismGroup>(post).Group, doorMover.Group, "the switch drives the door");
            using (var q = EM.CreateEntityQuery(typeof(Mechanism)))
            using (var ms = q.ToEntityArray(Allocator.Temp))
            {
                bool found = false;
                foreach (var m in ms) found |= EM.GetComponentData<Mechanism>(m).Group == doorMover.Group;
                Assert.IsTrue(found, "with the blast door's mechanism in its group");
            }
            TrackSpawnService.Despawn(EM, Group);
            using (var q = EM.CreateEntityQuery(typeof(PortalLink))) Assert.AreEqual(0, q.CalculateEntityCount(), "despawn clears the portal");
        }
    }
}
