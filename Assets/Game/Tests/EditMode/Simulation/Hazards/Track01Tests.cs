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
    }
}
