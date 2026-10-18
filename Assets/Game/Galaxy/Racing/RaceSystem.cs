using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Hazards;
using Game.Simulation.Commands;
using Game.Simulation.Hazards;
using Game.Voxel.Damage;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Galaxy.Racing
{
    /// <summary>SPEC-92 §5.1 lap rules, pure so tests drive them directly (RACE-005).</summary>
    public static class RaceRules
    {
        public const uint CountdownTicks = 180, RespawnDelayTicks = 180, FinishWindowTicks = 3600;

        static bool Allows(ulong mask, int gate) => gate >= 0 && gate < 64 && (mask & (1UL << gate)) != 0;

        /// <summary>One gate crossing by one racer. Returns true when it completed a lap.</summary>
        public static bool Cross(ref RacerProgress r, int gate, sbyte crossed, DynamicBuffer<RaceGate> gates, uint tick, byte laps, ref uint firstFinish)
        {
            if ((r.Flags & RacerFlags.Finished) != 0 || gate < 0 || gate >= gates.Length) return false;
            if (crossed < 0)
            {
                if (gate == r.LastGate) r.Flags |= RacerFlags.WrongWay;   // back over the gate just passed
                return false;
            }
            if ((r.Flags & RacerFlags.Started) == 0)
            {
                // The grid sits behind gate 0: the first crossing of it starts the race line; no other gate counts before it.
                if (gate != 0) return false;
                r.Flags |= RacerFlags.Started; r.LastGate = 0; r.GatesThisLap = 0; r.Flags &= ~RacerFlags.WrongWay;
                return false;
            }
            if (gate == r.LastGate) { r.Flags &= ~RacerFlags.WrongWay; return false; }   // forward again over the gate it backed over
            if (r.LastGate < 0 || !Allows(gates[r.LastGate].NextMask, gate)) return false;   // a cut or a wrong branch: not counted
            r.Flags &= ~RacerFlags.WrongWay;
            r.LastGate = (short)gate;
            if (gate != 0) { r.GatesThisLap++; return false; }
            uint lap = tick - r.LapStartTick;
            r.LastLapTicks = lap; if (r.BestLapTicks == 0 || lap < r.BestLapTicks) r.BestLapTicks = lap;
            r.Lap++; r.LapStartTick = tick; r.GatesThisLap = 0;
            if (r.Lap >= laps)
            {
                r.Flags |= RacerFlags.Finished; r.FinishTick = tick;
                if (firstFinish == 0) firstFinish = tick;
            }
            return true;
        }

        /// <summary>Order key: finished first by finish tick, then lap, gates this lap, and distance to the next gate (SPEC-92 §5.1.4).</summary>
        public static void Places(DynamicBuffer<RacerProgress> racers, NativeArray<float> distanceToNext)
        {
            int n = racers.Length;
            for (int i = 0; i < n; i++)
            {
                int place = 1; var a = racers[i];
                for (int j = 0; j < n; j++) if (j != i && Ahead(racers[j], distanceToNext[j], a, distanceToNext[i], j < i)) place++;
                a.Place = (byte)place; racers[i] = a;
            }
        }
        static bool Ahead(in RacerProgress b, float db, in RacerProgress a, float da, bool bFirstOnTie)
        {
            bool fb = (b.Flags & RacerFlags.Finished) != 0, fa = (a.Flags & RacerFlags.Finished) != 0;
            if (fb != fa) return fb;
            if (fb) return b.FinishTick != a.FinishTick ? b.FinishTick < a.FinishTick : bFirstOnTie;
            bool sb = (b.Flags & RacerFlags.Started) != 0, sa = (a.Flags & RacerFlags.Started) != 0;
            if (sb != sa) return sb;
            if (b.Lap != a.Lap) return b.Lap > a.Lap;
            if (b.GatesThisLap != a.GatesThisLap) return b.GatesThisLap > a.GatesThisLap;
            if (db != da) return db < da;
            return bFirstOnTie;
        }
    }

    /// <summary>SPEC-92 §5.1 (RACE-005): the race decider on the authority. Phases and holds, gate progress from ZoneContacts, places,
    /// the finish window, and respawn requests for racers whose ship died. Racers are bound to their live ship by Pilot.Slot each
    /// tick, so a respawned ship is picked up without a handshake.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ZoneSystem))]
    public partial struct RaceSystem : ISystem
    {
        EntityQuery races, pilots;
        public void OnCreate(ref SystemState state)
        {
            races = state.GetEntityQuery(typeof(RaceState), typeof(RacerProgress), typeof(RaceGate), typeof(LocalTransform));
            pilots = state.GetEntityQuery(ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<Hulk>());
            state.RequireForUpdate(races); state.RequireForUpdate<SimClock>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            using var raceEntities = races.ToEntityArray(Allocator.Temp);
            var race = raceEntities[0];
            var rs = em.GetComponentData<RaceState>(race);
            float2 origin = em.GetComponentData<LocalTransform>(race).Position.xz;
            var racers = em.GetBuffer<RacerProgress>(race);
            var gates = em.GetBuffer<RaceGate>(race);

            // Bind each racer to its pilot's live ship.
            using (var ships = pilots.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < racers.Length; i++)
                {
                    var r = racers[i]; Entity live = Entity.Null;
                    foreach (var s in ships) if (em.GetComponentData<Pilot>(s).Slot == r.Slot) { live = s; break; }
                    if (live != Entity.Null && live != r.Ship && (r.Flags & RacerFlags.Respawning) != 0) r.Flags &= ~(RacerFlags.Respawning | RacerFlags.WrongWay);
                    r.Ship = live;
                    racers[i] = r;
                }

            bool hold = rs.Phase == RacePhase.Lobby || rs.Phase == RacePhase.Countdown;
            if (rs.Phase == RacePhase.Countdown && tick - rs.PhaseTick >= RaceRules.CountdownTicks)
            {
                rs.Phase = RacePhase.Racing; rs.PhaseTick = tick; rs.StartTick = tick; rs.FirstFinishTick = 0; hold = false;
                for (int i = 0; i < racers.Length; i++) { var r = racers[i]; r.LapStartTick = tick; r.LastGate = -1; r.Lap = 0; r.Flags = 0; r.GatesThisLap = 0; racers[i] = r; }
            }
            for (int i = 0; i < racers.Length; i++)
            {
                var ship = racers[i].Ship;
                if (ship != Entity.Null && em.HasComponent<RaceHold>(ship) && em.IsComponentEnabled<RaceHold>(ship) != hold) em.SetComponentEnabled<RaceHold>(ship, hold);
            }

            if (rs.Phase == RacePhase.Racing)
            {
                uint firstFinish = rs.FirstFinishTick;
                if (SystemAPI.TryGetSingleton<ZoneContacts>(out var zc) && zc.Current.IsCreated)
                    foreach (var c in zc.Current)
                    {
                        if (c.Kind != ZoneEffectKind.Gate || c.Crossed == 0) continue;
                        for (int i = 0; i < racers.Length; i++)
                        {
                            if (racers[i].Ship != c.Body) continue;
                            var r = racers[i]; RaceRules.Cross(ref r, c.Id, c.Crossed, gates, tick, rs.Laps, ref firstFinish); racers[i] = r;
                        }
                    }
                rs.FirstFinishTick = firstFinish;

                // Death: a racer with no live ship waits RespawnDelayTicks, then asks the session to put it back at its last gate.
                for (int i = 0; i < racers.Length; i++)
                {
                    var r = racers[i];
                    if ((r.Flags & RacerFlags.Finished) != 0) continue;
                    if (r.Ship == Entity.Null && (r.Flags & RacerFlags.Respawning) == 0) { r.Flags |= RacerFlags.Respawning; r.RespawnAtTick = tick + RaceRules.RespawnDelayTicks; }
                    if ((r.Flags & RacerFlags.Respawning) != 0 && r.RespawnAtTick != 0 && tick >= r.RespawnAtTick)
                    {
                        var g = gates[math.max(0, (int)r.LastGate)];
                        var request = em.CreateEntity(typeof(RaceRespawnRequest));
                        em.SetComponentData(request, new RaceRespawnRequest { Slot = r.Slot, Position = origin + g.RespawnPosition, Yaw = g.RespawnYaw });
                        r.RespawnAtTick = 0;   // posted once; the session spawns the ship and the bind above clears Respawning
                    }
                    racers[i] = r;
                }

                bool all = racers.Length > 0;
                for (int i = 0; i < racers.Length; i++) all &= (racers[i].Flags & RacerFlags.Finished) != 0;
                if (all || rs.FirstFinishTick != 0 && tick - rs.FirstFinishTick >= RaceRules.FinishWindowTicks) { rs.Phase = RacePhase.Finished; rs.PhaseTick = tick; }
            }

            // Places, from progress and the distance to the nearest gate that may come next.
            using (var distance = new NativeArray<float>(racers.Length, Allocator.Temp))
            {
                var d = distance;
                for (int i = 0; i < racers.Length; i++)
                {
                    var r = racers[i]; float best = float.MaxValue;
                    if (r.Ship != Entity.Null)
                    {
                        float2 p = em.GetComponentData<LocalTransform>(r.Ship).Position.xz;
                        ulong mask = r.LastGate >= 0 ? gates[r.LastGate].NextMask : 1UL;
                        for (int g = 0; g < gates.Length && g < 64; g++) if ((mask & (1UL << g)) != 0) best = math.min(best, math.distance(p, origin + gates[g].Position));
                    }
                    d[i] = best;
                }
                RaceRules.Places(racers, d);
            }
            em.SetComponentData(race, rs);
            // HAZ-014: the leader's lap for RaceLap mechanism triggers (ADR-0119 decision 9: places change by lap).
            if (SystemAPI.TryGetSingletonRW<Game.Simulation.Mechanisms.RaceLapSignal>(out var signal))
            {
                int leader = 0; Entity first = Entity.Null;
                if (rs.Phase == RacePhase.Racing)
                    for (int i = 0; i < racers.Length; i++)
                    {
                        leader = math.max(leader, racers[i].Lap);
                        if (racers[i].Place == 1 && (racers[i].Flags & RacerFlags.Finished) == 0) first = racers[i].Ship;
                    }
                signal.ValueRW.LeaderLap = leader; signal.ValueRW.Leader = first;   // HAZ-020: the leader gauntlet turrets shoot
            }
        }
    }
}
