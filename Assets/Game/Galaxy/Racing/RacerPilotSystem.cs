using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Hazards;
using Game.Simulation.Power;
using Game.Data.Combat;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Galaxy.Racing
{
    /// <summary>RACE-017: the racer AI's plan. From the gate a racer last passed it picks the branches ahead (the shortest way round,
    /// or now and then another when its skill is low) and joins those legs of the racing line into its line. Pure, main thread.</summary>
    public static class RacerPlan
    {
        public const int LegsAhead = 4;
        public const float GridApproach = 300;   // the line starts this far behind gate 0 for a racer on the grid
        public const float FeasibleSpan = 1500;  // m of a branch checked for corners it could not make

        /// <summary>What a racer's ship can do, for planning: its speed now, and the side and braking accelerations it can count on.</summary>
        /// <summary>Env set: corners from the hull's grip at each speed (PilotEnvelope.CornerSpeed) with Share of it; else a fixed Lateral.</summary>
        public struct Ability { public float Speed, Lateral, Brake, YawCap, Share, Reach; public PilotEnvelope Env; }   // Reach: m its hull keeps off a fence (0 = unsized)

        /// <summary>How near a route's edge a hull may run: a field wall pushes a body from WallRadiusFraction of its bounding sphere off
        /// the wall line, plus CorridorMargin. The steering job uses the same rule with the spatial hash's sphere.</summary>
        public static float EdgeReach(float radius, float hull) => math.max(radius, ZoneSystem.WallRadiusFraction * hull + 3) + RacerSteerJob.CorridorMargin;

        /// <summary>From the hull's envelope: its grip is the drift grip (main engine and strafe together) it has at each speed, a little
        /// held back for response lag; a better pilot uses more of it.</summary>
        public static Ability AbilityOf(in PilotEnvelope env, in FlightConfig config, float skill, float speed) => new Ability
        {
            Speed = speed, YawCap = env.YawRate, Env = env,
            Share = (.72f + .16f * math.saturate(skill)) / (1 + env.Spool),
            Lateral = env.BestGrip * (.72f + .16f * math.saturate(skill)) / (1 + env.Spool),
            // the engines, but no faster than a push (a boost strip's) bleeds off under Brake (ADR-0118 carry)
            Brake = math.min(env.Reverse, config.CarryBrakeDecel > 0 ? config.CarryBrakeDecel : 1e6f) * .9f,
        };

        /// <summary>The fastest it may take a turn of radius r.</summary>
        public static float CornerSpeed(in Ability a, float r) =>
            math.min(a.Env.Forward > 0 ? a.Env.CornerSpeed(r, a.Share) : math.sqrt(a.Lateral * r), a.YawCap * .85f * r);

        /// <summary>Whether a racer at its speed now can make the corners of a leg's first FeasibleSpan metres, braking all it can. Boost
        /// strips cover half the route and can be left alone, so they do not count here (StripAvoid decides whether to take one).</summary>
        public static bool Feasible(DynamicBuffer<RaceLegPoint> points, in RaceLeg leg, in Ability a) => Excess(points, leg, a) <= 1;

        /// <summary>How far over its corner limits (with 15% grace) a racer would be on a leg's first FeasibleSpan metres: ≤ 1 makeable.</summary>
        public static float Excess(DynamicBuffer<RaceLegPoint> points, in RaceLeg leg, in Ability a)
        {
            float v = a.Speed, walked = 0, worst = 0;
            for (int i = 1; i < leg.Count && walked < FeasibleSpan; i++)
            {
                var p = points[leg.Start + i];
                float ds = math.distance(points[leg.Start + i - 1].Position, p.Position); walked += ds;
                v = math.sqrt(math.max(0, v * v - 2 * a.Brake * ds));
                float r = Radius(points[leg.Start + math.max(0, i - 3)].Position, p.Position, points[leg.Start + math.min(leg.Count - 1, i + 3)].Position);
                worst = math.max(worst, v / (CornerSpeed(a, r) * 1.15f + 5));
                // and a route too narrow for the hull (a heavy racer down the 70 m breach banged both fences every lap)
                if (a.Reach > 0) worst = math.max(worst, a.Reach / math.max(1, p.HalfWidth));
            }
            return worst;
        }

        public static float Radius(float2 a, float2 b, float2 c)
        {
            float2 ab = b - a, bc = c - b;
            float s = math.length(ab) + math.length(bc), turn = 0;
            if (math.lengthsq(ab) > 1e-4f && math.lengthsq(bc) > 1e-4f) turn = math.acos(math.clamp(math.dot(math.normalize(ab), math.normalize(bc)), -1, 1));
            // the turn happens between the chords' midpoints, half their summed length apart (the whole length read every corner as
            // twice as wide and planned it √2 too fast)
            return turn > 1e-3f ? s * .5f / turn : 1e5f;
        }

        /// <summary>Distance from each gate to gate 0 along the shortest legs (gate 0: 0).</summary>
        public static float[] ToFinish(DynamicBuffer<RaceLeg> legs, int gates)
        {
            var cost = new float[gates];
            for (int g = 1; g < gates; g++) cost[g] = float.MaxValue;
            for (int pass = 0; pass < gates; pass++)
                foreach (var leg in legs)
                {
                    if (leg.From == 0 || leg.From >= gates || leg.To >= gates) continue;
                    float to = leg.To == 0 ? 0 : cost[leg.To];
                    if (to < float.MaxValue && leg.Length + to < cost[leg.From]) cost[leg.From] = leg.Length + to;
                }
            return cost;
        }

        /// <summary>The leg (index into legs) a racer takes on from gate `from`, or −1: the shortest way round among the branches it can
        /// make at its speed (any, if it can make none), now and then another of those when its skill is low. The same racer, gate, lap
        /// and speed class always choose the same, so it does not waver.</summary>
        public static int Choose(DynamicBuffer<RaceLeg> legs, DynamicBuffer<RaceLegPoint> points, float[] toFinish, int from, uint seed, float skill, int lap, in Ability ability)
        {
            int feasible = 0, leastBad = -1; float leastExcess = float.MaxValue;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i].From != from) continue;
                float excess = Excess(points, legs[i], ability);
                if (excess <= 1) feasible++;
                if (excess < leastExcess) { leastExcess = excess; leastBad = i; }
            }
            if (feasible == 0) return leastBad;   // none it can make cleanly: the one it overshoots least
            int best = -1, options = 0; float bestCost = float.MaxValue;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i].From != from || !Feasible(points, legs[i], ability)) continue;
                options++;
                float rest = legs[i].To == 0 ? 0 : toFinish[legs[i].To];
                float c = legs[i].Length + (rest == float.MaxValue ? 1e6f : rest);
                if (c < bestCost) { bestCost = c; best = i; }
            }
            if (options < 2) return best;
            uint h = math.hash(new uint3(seed, (uint)from, (uint)lap));
            if ((h & 0xffff) / 65536f >= (1 - skill) * .6f) return best;
            int pick = (int)((h >> 16) % (uint)options);
            for (int i = 0; i < legs.Length; i++)
                if (legs[i].From == from && Feasible(points, legs[i], ability) && pick-- == 0) return i;
            return best;
        }

        /// <summary>The leg from gate `from` whose line passes nearest `p` (track space), and how near.</summary>
        public static int NearestLeg(DynamicBuffer<RaceLeg> legs, DynamicBuffer<RaceLegPoint> points, int from, float2 p, out float distance)
        {
            int best = -1; distance = float.MaxValue;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i].From != from) continue;
                for (int k = legs[i].Start; k < legs[i].Start + legs[i].Count; k++)
                {
                    float d = math.distance(points[k].Position, p);
                    if (d < distance) { distance = d; best = i; }
                }
            }
            return best;
        }

        /// <summary>Returns the gate the line ends at (−1: no line).</summary>
        public static int Build(List<RacerLinePoint> line, DynamicBuffer<RaceGate> gates, DynamicBuffer<RaceLeg> legs, DynamicBuffer<RaceLegPoint> points,
            int from, uint seed, float skill, int lap, in Ability ability, int firstLeg = -1)
        {
            line.Clear();
            if (gates.Length == 0 || legs.Length == 0) return -1;
            var toFinish = ToFinish(legs, gates.Length);
            int at = from;
            if (from < 0)
            {
                var g = gates[0];
                line.Add(new RacerLinePoint { Position = g.Position - new float2(math.sin(g.Yaw), math.cos(g.Yaw)) * GridApproach, HalfWidth = g.Width * .5f });
                at = 0;
            }
            for (int k = 0; k < LegsAhead; k++)
            {
                if (k > 0 && at == 0) lap++;   // past the line again: the next lap's choices
                int leg = k == 0 && firstLeg >= 0 && legs[firstLeg].From == at ? firstLeg : Choose(legs, points, toFinish, at, seed, skill, lap, ability);
                if (leg < 0) break;
                var l = legs[leg];
                for (int i = 0; i < l.Count; i++)
                {
                    var p = points[l.Start + i];
                    if (line.Count > 0 && math.distancesq(line[line.Count - 1].Position, p.Position) < 1) continue;
                    line.Add(new RacerLinePoint { Position = p.Position, HalfWidth = p.HalfWidth, PushSpeed = p.PushSpeed, PushSide = p.PushSide });
                }
                at = l.To;
            }
            // Turn radius at each point from the heading change across ±3 points.
            for (int i = 0; i < line.Count; i++)
            {
                var p = line[i]; p.Radius = Radius(line[math.max(0, i - 3)].Position, p.Position, line[math.min(line.Count - 1, i + 3)].Position); line[i] = p;
            }
            // Each boost strip on the line: take it only if, leaving it at its push speed, it can still make the bends after it.
            for (int i = 0; i < line.Count; i++)
            {
                if (line[i].PushSpeed <= 0) continue;
                int end = i; while (end + 1 < line.Count && line[end + 1].PushSpeed > 0) end++;
                byte avoid = (byte)(StripTooFast(line, end, line[i].PushSpeed, ability) ? 1 : 0);
                for (int k = i; k <= end; k++) { var p = line[k]; p.Avoid = avoid; line[k] = p; }
                i = end;
            }
            return line.Count > 1 ? at : -1;
        }

        /// <summary>Whether a racer leaving a strip at line point `exit` at `speed` would overshoot a bend within FeasibleSpan after it.
        /// The line ending soon after counts as clear (it is rebuilt at the next gate with the next legs).</summary>
        public static bool StripTooFast(List<RacerLinePoint> line, int exit, float speed, in Ability a)
        {
            float v = speed, walked = 0;
            for (int i = exit + 1; i < line.Count && walked < FeasibleSpan; i++)
            {
                float ds = math.distance(line[i - 1].Position, line[i].Position); walked += ds;
                v = math.sqrt(math.max(0, v * v - 2 * a.Brake * ds));
                if (v > CornerSpeed(a, line[i].Radius) * 1.1f + 3) return true;
            }
            return false;
        }
    }

    /// <summary>RACE-017: flies every RacerPilot ship round the race. On the main thread it keeps each racer's line (rebuilt when it
    /// passes a gate) and puts a stuck racer back at its last gate. A Burst job then steers: it follows the line to a point ahead,
    /// slows for the corners it will reach, and scores headings around that point against what is ahead: other ships, hulks and mines
    /// (mines by their trigger reach) from the spatial hash, and solid structure (wreck hulls, posts, pylons) from sphere casts.
    /// Writes ShipCommand after the player and remote-pilot commands, so it also flies a player's ship under `race autopilot`.</summary>
    [UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(RemotePilotApplySystem))]
    public partial struct RacerPilotSystem : ISystem
    {
        EntityQuery racers, races;
        ComponentLookup<FlightState> flights;
        ComponentLookup<PropulsionStats> propulsion;
        ComponentLookup<VoxelGrid> grids;
        ComponentLookup<PhysicsVelocity> velocities;
        ComponentLookup<RaceHold> holds;
        ComponentLookup<MineFuse> mines;
        ComponentLookup<GridSpawning> spawning;
        BufferLookup<FlightThruster> thrusters;
        ComponentLookup<Pilot> pilots;
        ComponentLookup<WeaponStats> weapons;
        ComponentLookup<ShipStatMultipliers> multipliers;
        ComponentLookup<ShieldState> shields;

        public void OnCreate(ref SystemState state)
        {
            racers = state.GetEntityQuery(ComponentType.ReadWrite<RacerPilot>(), ComponentType.ReadWrite<ShipCommand>(), ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<RacerLinePoint>(), ComponentType.Exclude<Hulk>());
            races = state.GetEntityQuery(typeof(RaceState), typeof(RacerProgress), typeof(RaceGate), typeof(RaceLeg), typeof(RaceLegPoint), typeof(LocalTransform));
            state.RequireForUpdate(racers); state.RequireForUpdate(races);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<FlightConfig>();
            state.RequireForUpdate<SpatialHash>(); state.RequireForUpdate<PhysicsWorldSingleton>();
            flights = state.GetComponentLookup<FlightState>(true); propulsion = state.GetComponentLookup<PropulsionStats>(true);
            grids = state.GetComponentLookup<VoxelGrid>(true); velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            holds = state.GetComponentLookup<RaceHold>(true); mines = state.GetComponentLookup<MineFuse>(true);
            spawning = state.GetComponentLookup<GridSpawning>(true);
            thrusters = state.GetBufferLookup<FlightThruster>(true); pilots = state.GetComponentLookup<Pilot>(true); weapons = state.GetComponentLookup<WeaponStats>(true);
            multipliers = state.GetComponentLookup<ShipStatMultipliers>(true); shields = state.GetComponentLookup<ShieldState>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            using var raceEntities = races.ToEntityArray(Allocator.Temp);
            var race = raceEntities[0];
            var rs = em.GetComponentData<RaceState>(race);
            float2 origin = em.GetComponentData<LocalTransform>(race).Position.xz;
            var config = SystemAPI.GetSingleton<FlightConfig>();

            // Lines and resets on the main thread: a few racers, rebuilt only at gates.
            using (var ships = racers.ToEntityArray(Allocator.Temp))
                foreach (var ship in ships)
                {
                    var pilot = em.GetComponentData<RacerPilot>(ship);
                    byte slot = em.GetComponentData<Pilot>(ship).Slot;
                    var progress = em.GetBuffer<RacerProgress>(race);
                    short from = -1; byte lap = 0; bool found = false;
                    for (int i = 0; i < progress.Length; i++)
                        if (progress[i].Slot == slot) { from = (progress[i].Flags & RacerFlags.Started) != 0 ? progress[i].LastGate : (short)-1; lap = progress[i].Lap; found = true; }
                    if (!found) continue;
                    if (pilot.WantsReset != 0)
                    {
                        var g = em.GetBuffer<RaceGate>(race)[math.max(0, (int)from)];
                        var request = em.CreateEntity(typeof(RaceRespawnRequest));
                        em.SetComponentData(request, new RaceRespawnRequest { Slot = slot, Position = origin + g.RespawnPosition, Yaw = g.RespawnYaw, Ship = ship });
                        pilot.WantsReset = 0; pilot.Index = 0; pilot.ProgressTick = tick;
                        pilot.LineFrom = -2;   // the line is rebuilt from the gate it is put back at
                    }
                    int length = em.GetBuffer<RacerLinePoint>(ship).Length;
                    bool fresh = pilot.LineFrom != from || pilot.LineLap != lap || length < 2;
                    // At the end of its line with no new gate counted (a finished racer cruising on): carry on from where it ends.
                    bool carryOn = !fresh && pilot.LineTo >= 0 && pilot.Index >= length - 25;
                    // Pushed onto another branch (a strip, traffic, a hit): change its mind rather than fight back across the field.
                    int switchTo = -1;
                    if (!fresh && !carryOn && pilot.Off > 25 && tick % 10 == 0 && tick >= pilot.CommitUntil)
                    {
                        float2 at = em.GetComponentData<LocalTransform>(ship).Position.xz - origin;
                        int leg = RacerPlan.NearestLeg(em.GetBuffer<RaceLeg>(race), em.GetBuffer<RaceLegPoint>(race), math.max(0, (int)from), at, out float d);
                        if (leg >= 0 && d < pilot.Off * .5f) { switchTo = leg; pilot.CommitUntil = tick + 180; }   // then hold to it for 3 s
                    }
                    if (fresh || carryOn || switchTo >= 0)
                    {
                        var built = new List<RacerLinePoint>();
                        float speed = em.HasComponent<PhysicsVelocity>(ship) ? math.length(em.GetComponentData<PhysicsVelocity>(ship).Linear.xz) : 0;
                        var flightNow = em.HasComponent<FlightState>(ship) ? em.GetComponentData<FlightState>(ship) : new FlightState { ForwardCap = 120, StrafeCap = 72, ReverseCap = 60, YawRateCap = 2 };
                        var envelope = PilotEnvelope.From(em.HasComponent<PropulsionStats>(ship) ? em.GetComponentData<PropulsionStats>(ship) : default, flightNow, config, false, 0);
                        var ability = RacerPlan.AbilityOf(envelope, config, pilot.Skill, speed);
                        if (em.HasComponent<VoxelGrid>(ship))
                        {
                            var g = em.GetComponentData<VoxelGrid>(ship);
                            float hw = math.max(2, (g.CellMax.x - g.CellMin.x + 1) * g.CellSize * .5f), hl = math.max(2, (g.CellMax.z - g.CellMin.z + 1) * g.CellSize * .5f);
                            ability.Reach = RacerPlan.EdgeReach(math.max(hw, hl * .6f), math.sqrt(hw * hw + hl * hl));
                        }
                        pilot.LineTo = (short)RacerPlan.Build(built, em.GetBuffer<RaceGate>(race), em.GetBuffer<RaceLeg>(race), em.GetBuffer<RaceLegPoint>(race),
                            carryOn ? pilot.LineTo : from, pilot.Seed, pilot.Skill, carryOn ? lap + 1 + (int)(tick & 7) : lap, ability, switchTo);
                        var line = em.GetBuffer<RacerLinePoint>(ship);
                        line.Clear();
                        foreach (var p in built) line.Add(p);
                        pilot.LineFrom = from; pilot.LineLap = lap; pilot.Index = 0; pilot.Steer = 0; pilot.Replans++; pilot.ProgressTick = tick;
                        // start on the segment nearest the ship (a switched line can be joined part way along)
                        float2 here = em.GetComponentData<LocalTransform>(ship).Position.xz - origin; float best = float.MaxValue;
                        for (int i = 0; i + 1 < line.Length && (i < 40 || switchTo >= 0); i++)
                        {
                            float d = SegmentDistanceSq(here, line[i].Position, line[i + 1].Position, out _);
                            if (d < best) { best = d; pilot.Index = i; }
                        }
                        pilot.ProgressIndex = pilot.Index;
                    }
                    em.SetComponentData(ship, pilot);
                }

            flights.Update(ref state); propulsion.Update(ref state); grids.Update(ref state); velocities.Update(ref state);
            holds.Update(ref state); mines.Update(ref state); spawning.Update(ref state);
            thrusters.Update(ref state); pilots.Update(ref state); weapons.Update(ref state); multipliers.Update(ref state); shields.Update(ref state);
            bool done(RacerFlags f) => (f & RacerFlags.Finished) != 0;
            var finished = new NativeHashSet<byte>(4, state.WorldUpdateAllocator);
            foreach (var r in em.GetBuffer<RacerProgress>(race)) if (done(r.Flags)) finished.Add(r.Slot);
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            var job = new RacerSteerJob
            {
                Origin = origin, Tick = tick, Racing = (byte)(rs.Phase == RacePhase.Racing ? 1 : 0), Config = SystemAPI.GetSingleton<FlightConfig>(),
                Hash = hash.AsReadOnly(), Collision = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld, Finished = finished,
                Boosts = em.HasBuffer<RaceBoost>(race) ? em.GetBuffer<RaceBoost>(race).ToNativeArray(state.WorldUpdateAllocator) : new NativeArray<RaceBoost>(0, state.WorldUpdateAllocator),
                Flights = flights, Propulsion = propulsion, Grids = grids, Velocities = velocities, Holds = holds, Mines = mines, Spawning = spawning, Thrusters = thrusters, Pilots = pilots, Weapons = weapons,
                Multipliers = multipliers, Shields = shields,
            };
            var handle = job.ScheduleParallel(racers, hash.BeginRead(state.Dependency));
            hash.EndRead(handle);
            state.Dependency = handle;
        }

        /// <summary>Puts the racer AI in charge of a ship (main thread, a structural change).</summary>
        public static void Attach(EntityManager em, Entity ship, float skill, uint seed)
        {
            // its lane: a share of the free width either side of the centreline, so a pack spreads out
            float lane = ((math.hash(new uint2(seed, 91)) & 1023) / 1023f - .5f) * .6f;
            float aggression = .25f + .5f * ((math.hash(new uint2(seed, 17)) & 1023) / 1023f);
            em.AddComponentData(ship, new RacerPilot { Skill = skill, Seed = seed, LineFrom = -2, Lane = lane, Aggression = aggression });
            if (!em.HasBuffer<RacerLinePoint>(ship)) em.AddBuffer<RacerLinePoint>(ship);
        }

        internal static float SegmentDistanceSq(float2 p, float2 a, float2 b, out float t)
        {
            float2 ab = b - a; float len = math.lengthsq(ab);
            t = len > 1e-6f ? math.saturate(math.dot(p - a, ab) / len) : 0;
            return math.distancesq(p, a + ab * t);
        }
    }

    /// <summary>RACE-017: one racer's command for this tick. See RacerPilotSystem.</summary>
    [BurstCompile]
    public partial struct RacerSteerJob : IJobEntity
    {
        public float2 Origin; public uint Tick; public byte Racing; public FlightConfig Config;
        [ReadOnly] public SpatialHashQueryData Hash;
        [ReadOnly] public CollisionWorld Collision;
        [ReadOnly] public NativeHashSet<byte> Finished;
        [ReadOnly] public NativeArray<RaceBoost> Boosts;
        [ReadOnly] public ComponentLookup<FlightState> Flights;
        [ReadOnly] public ComponentLookup<PropulsionStats> Propulsion;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<RaceHold> Holds;
        [ReadOnly] public ComponentLookup<MineFuse> Mines;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public BufferLookup<FlightThruster> Thrusters;
        [ReadOnly] public ComponentLookup<Pilot> Pilots;
        [ReadOnly] public ComponentLookup<WeaponStats> Weapons;
        [ReadOnly] public ComponentLookup<ShipStatMultipliers> Multipliers;
        [ReadOnly] public ComponentLookup<ShieldState> Shields;

        // Headings tried around the line target (rad): straight first, then wider either side.
        static readonly float[] Offsets = { 0, .1f, -.1f, .21f, -.21f, .35f, -.35f, .52f, -.52f, .73f, -.73f, .98f, -.98f, 1.3f, -1.3f };
        public const float LineUpNose = .12f;    // rad: the most it drifts while lining up a shot at a mine in its way
        public const float Horizon = 3;          // s: how far ahead other bodies are predicted
        public const float Margin = 5;           // m kept from anything, on top of both radii
        public const float CorridorMargin = 10;  // m kept inside the route's edge (the field walls)
        public const float MineShootMin = 70, MineShootMax = 380;   // m: outside a mine's 28 m blast, inside gun range
        public const float MineShootArc = .3f;                     // rad off the nose (fixed guns converge on the aim point)
        public const float RivalShootArc = .2f;                    // rad off the nose for a rival
        public const float StripLook = 450;                        // m ahead it settles which half of the route to take for a strip

        enum ObstacleKind : byte { Other = 0, Mine = 1, Rival = 2, Debris = 3 }
        struct Obstacle { public float2 Position, Velocity; public float Radius; public ObstacleKind Kind; public bool Mine => Kind == ObstacleKind.Mine; }

        /// <summary>Whether another ship lies in the line of fire, nearer than the target.</summary>
        static bool LineBlocked(in FixedList4096Bytes<Obstacle> near, int target, float2 pos, float2 rel, float d)
        {
            for (int j = 0; j < near.Length; j++)
            {
                if (j == target || near[j].Kind != ObstacleKind.Rival) continue;
                float2 r2 = near[j].Position - pos;
                if (math.length(r2) < d && math.dot(math.normalizesafe(r2), math.normalizesafe(rel)) > math.cos(.15f)) return true;
            }
            return false;
        }

        void Execute(Entity entity, ref RacerPilot pilot, ref ShipCommand command, in LocalTransform pose, in Pilot slot, in DynamicBuffer<RacerLinePoint> line)
        {
            if (Holds.HasComponent(entity) && Holds.IsComponentEnabled(entity))
            {
                command = PilotCommandConversion.Hold(default); pilot.ProgressTick = Tick; pilot.TargetSpeed = 0; pilot.Threat = 0;
                return;
            }
            if (Spawning.HasComponent(entity) && Spawning.IsComponentEnabled(entity)) return;
            if (line.Length < 2 || !Flights.HasComponent(entity) || !Velocities.HasComponent(entity)) return;
            var flight = Flights[entity];
            if (flight.ForwardCap <= 0 || flight.YawRateCap <= 0) return;
            float3 world = pose.Position;
            float2 pos = world.xz - Origin, vel = Velocities[entity].Linear.xz;
            float speed = math.length(vel);
            float yaw = Planar.YawFromForward(math.rotate(pose.Rotation, new float3(0, 0, 1)));
            float2 forward = new float2(math.sin(yaw), math.cos(yaw));

            // Size: half width across, half length along (m).
            float halfWidth = 4, halfLength = 6;
            if (Grids.HasComponent(entity))
            {
                var g = Grids[entity];
                halfWidth = math.max(2, (g.CellMax.x - g.CellMin.x + 1) * g.CellSize * .5f);
                halfLength = math.max(2, (g.CellMax.z - g.CellMin.z + 1) * g.CellSize * .5f);
            }
            float radius = math.max(halfWidth, halfLength * .6f);
            int own = Hash.FindIndex(entity);
            float hull = own >= 0 ? Hash.Get(own).HullRadius : radius;   // its bounding sphere: mines and field walls measure to it
            // How near the line's edge it may run: a field wall pushes a body from WallRadiusFraction of its sphere off the wall line.
            float edgeReach = RacerPlan.EdgeReach(radius, hull);

            // Where on the line: the nearest segment a little behind to well ahead; anywhere if it is far off.
            int index = math.clamp(pilot.Index, 0, line.Length - 2);
            float best = float.MaxValue, bestT = 0;
            for (int i = math.max(0, index - 3); i <= math.min(line.Length - 2, index + 40); i++)
            {
                float d = RacerPilotSystem.SegmentDistanceSq(pos, line[i].Position, line[i + 1].Position, out float t);
                if (d < best) { best = d; pilot.Index = i; bestT = t; }
            }
            float lineHalf = line[pilot.Index].HalfWidth;
            if (best > (lineHalf * 3 + 100) * (lineHalf * 3 + 100))
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    float d = RacerPilotSystem.SegmentDistanceSq(pos, line[i].Position, line[i + 1].Position, out float t);
                    if (d < best) { best = d; pilot.Index = i; bestT = t; }
                }
            float offLine = math.sqrt(best);

            // Speed: the corner limit of every point it will reach before it could brake, through the braking it has.
            // A boost strip ahead gives no room to brake: it pushes harder than any engine brakes.
            var stats = Propulsion.HasComponent(entity) ? Propulsion[entity] : default;
            float skill = math.saturate(pilot.Skill);
            float speedMultiplier = FlightMath.SpeedMultiplier(Config, Multipliers.HasComponent(entity) ? Multipliers[entity].Get(StatId.MaxSpeed) : 1,
                Shields.HasComponent(entity) && Shields[entity].Active != 0);
            var envelope = PilotEnvelope.From(stats, Config, speedMultiplier, FlightMath.YawRateCap(Config, Multipliers.HasComponent(entity) ? Multipliers[entity].Get(StatId.TurnRate) : 1),
                Thrusters.HasBuffer(entity) ? PilotEnvelope.ForwardSpool(Thrusters[entity]) : 0);
            var ability = RacerPlan.AbilityOf(envelope, Config, skill, speed);
            float brake = ability.Brake;
            float baseCap = envelope.Cruise, boostCap = envelope.Boost;
            float yawCap = flight.YawRateCap;
            float target = boostCap, reach = speed * speed / (2 * brake) + 80, along = -bestT * math.distance(line[pilot.Index].Position, line[pilot.Index + 1].Position), room = along;
            for (int i = pilot.Index; i < line.Length && along < reach + 600; i++)
            {
                if (i > pilot.Index)
                {
                    float ds = math.distance(line[i - 1].Position, line[i].Position);
                    along += ds; if (line[i].PushSpeed <= 0 || line[i].Avoid != 0) room += ds;   // no braking on a strip it takes
                }
                if (room > reach) break;
                float corner = RacerPlan.CornerSpeed(ability, line[i].Radius);
                target = math.min(target, math.sqrt(corner * corner + 2 * brake * math.max(0, room)));
            }
            if (pilot.Index + 2 >= line.Length) target = math.min(target, 20);   // the end of its line (it is rebuilt at the next gate)
            target *= .94f + .06f * skill;
            if (Racing == 0 || Finished.Contains(slot.Slot)) target = math.min(target, baseCap * .5f);

            // The point it steers for, ahead along the line, in its own lane. Nearer in a tight corner: a chord to a far point would
            // cut across the inside fence (its sagitta L²/8R kept within half the free width).
            float look = math.clamp(25 + speed * .8f, 40, 180), tightest = 1e5f, walked = 0;
            for (int i = pilot.Index + 1; i < line.Length && walked < look; i++) { walked += math.distance(line[i - 1].Position, line[i].Position); tightest = math.min(tightest, line[i].Radius); }
            float free = math.max(6, lineHalf - edgeReach);
            look = math.min(look, math.max(35, math.sqrt(8 * tightest * free * .5f)));
            // A boost strip coming up: its half of the route if it takes it, the other half if it keeps off it.
            float lane = pilot.Lane; bool avoidStrip = false; float stripWalk = 0;
            for (int i = pilot.Index; i < line.Length && stripWalk < StripLook; i++)
            {
                if (i > pilot.Index) stripWalk += math.distance(line[i - 1].Position, line[i].Position);
                if (line[i].PushSpeed <= 0) continue;
                avoidStrip = line[i].Avoid != 0 || Racing == 0 || Finished.Contains(slot.Slot);   // cruising after the flag: no strip flinging it into the field
                lane = avoidStrip ? -math.sign(line[i].PushSide) * .8f : math.sign(line[i].PushSide) * .55f;
                break;
            }
            // The way there must stay inside the route too: at a fork a long chord cut across the corner's fence post, the casts
            // turned the racer off it, and a heavy one swung past the branch and pinned itself on the far fence. Nearer until clear.
            float2 aim, aimDir; float aimHalf;
            for (int tries = 0; ; tries++)
            {
                aim = PointAlong(line, pilot.Index, bestT, look, out aimDir, out aimHalf);
                aim += new float2(aimDir.y, -aimDir.x) * lane * math.max(0, aimHalf - edgeReach);
                bool clear = true;
                for (int k = 1; k <= 2 && clear; k++)
                {
                    float2 q = math.lerp(pos, aim, k / 3f);
                    clear = LineDistance(line, pilot.Index, q, out float qHalf) <= math.max(6, qHalf - edgeReach * .5f);
                }
                if (clear || tries >= 4 || look <= 36) break;
                look = math.max(35, look * .7f);
            }
            float2 toAim = aim - pos;
            float aimYaw = math.lengthsq(toAim) > 1 ? math.atan2(toAim.x, toAim.y) : yaw;

            // What is around: ships, hulks, mines and loose debris; big static grids are left to the casts.
            var near = new FixedList4096Bytes<Obstacle>();
            var found = new FixedList512Bytes<int>();
            Hash.QuerySphere(world, math.min(450, speed * Horizon + 150), ref found);
            for (int k = 0; k < found.Length && near.Length < near.Capacity; k++)
            {
                var proxy = Hash.Get(found[k]);
                if (proxy.Entity == entity || proxy.Kind == ProxyKind.Celestial || proxy.Kind == ProxyKind.Station || proxy.HullRadius > 70) continue;
                bool mine = Mines.HasComponent(proxy.Entity);
                // a mine: its trigger reach to our bounding sphere; anything else: both bodies' sizes
                float r = mine ? Mines[proxy.Entity].Params.TriggerRadius + hull + Margin : proxy.HullRadius + radius + Margin;
                var kind = mine ? ObstacleKind.Mine : Pilots.HasComponent(proxy.Entity) && proxy.Kind == ProxyKind.Ship ? ObstacleKind.Rival
                    : proxy.Kind == ProxyKind.Hulk || proxy.Kind == ProxyKind.Asteroid ? ObstacleKind.Debris : ObstacleKind.Other;
                near.Add(new Obstacle { Position = proxy.Center.xz - Origin, Velocity = proxy.Velocity.xz, Radius = r, Kind = kind });
            }

            // Score each heading: how far it turns from the line, leaving the route, bodies it would meet, structure it would hit.
            float bestCost = float.MaxValue, bestOffset = 0, bestThreat = 0, bestMines = 0, bestClear = float.MaxValue;
            // three seconds out, to steer round a big wreck in time (at 1.4 s it saw hulks too late to turn and hit them at 100+ m/s); it brakes
            // only for what is within the nearer reach, since a long straight cast meets the outside of every bend
            float castLength = math.clamp(speed * 3 + 30, 50, 450), brakeReach = math.clamp(speed * 1.4f + 30, 50, 280), castRadius = math.clamp(halfWidth * .9f, 2, 14);
            for (int c = 0; c < Offsets.Length; c++)
            {
                float heading = aimYaw + Offsets[c];
                float2 u = new float2(math.sin(heading), math.cos(heading));
                // hysteresis, strongest when slow (where a jittering choice spins the nose round)
                float cost = (1 - math.cos(Offsets[c])) * 3 + math.abs(Offsets[c] - pilot.Steer) * (.4f + math.saturate(1 - speed / 60));
                // leaving the route, anywhere along the way to the aim point
                float span = math.min(look, 40 + speed * .6f);
                for (int k = 1; k <= 3; k++)
                {
                    float edge = LineDistance(line, pilot.Index, pos + u * (span * k / 3), out float half) - math.max(5, half - edgeReach);
                    if (edge > 0) cost += edge / math.max(10, half) * 3;
                    if (avoidStrip && OnStrip(pos + u * (span * k / 3), radius)) cost += 4;   // keep off a strip it cannot take
                }
                // bodies: closest approach within the horizon at the target speed
                float threat = 0, mines = 0;
                float2 v = u * math.max(target, 10);
                for (int k = 0; k < near.Length; k++)
                {
                    var o = near[k];
                    float2 rel = o.Position - pos, w = v - o.Velocity;
                    float t = math.clamp(math.dot(rel, w) / math.max(1e-3f, math.lengthsq(w)), 0, Horizon);
                    float miss = math.length(rel - w * t);
                    if (miss >= o.Radius) continue;
                    float danger = (1 - miss / o.Radius) * (1.5f - t / Horizon) * 4;
                    if (o.Mine) mines += danger * 1.5f; else threat += danger;
                }
                // structure: a sphere the ship's width swept ahead
                var collector = new NavHitCollector(entity, 1);
                float clear = float.MaxValue;
                if (Collision.SphereCastCustom(new float3(world.x, 0, world.z), castRadius, new float3(u.x, 0, u.y), castLength, ref collector, PhysicsLayers.NavigationQuery))
                {
                    clear = collector.Fraction * castLength;
                    float seconds = clear / math.max(15, speed);
                    threat += math.saturate(1 - seconds / 2.5f) * 8 + .5f;
                }
                cost += threat + mines;
                if (cost < bestCost) { bestCost = cost; bestOffset = Offsets[c]; bestThreat = threat; bestMines = mines; bestClear = clear; }
            }
            pilot.Steer = bestOffset; pilot.Aim = aim;
            // Guns (owner: "they should be able to shoot opponents and obstacles if necessary"). First anything it cannot pass clear of
            // on its heading — a mine from beyond its blast (knocked aside or set off safely), a hulk or a rock; then, in bursts as
            // aggressive as the racer is, a rival ahead in the guns' arc. Never through another ship; only while racing.
            float2 gunAt = default; bool shoot = false, lineUp = false;
            float range = Weapons.HasComponent(entity) ? math.min(Weapons[entity].MaxWeaponRange, MineShootMax) : 0;
            if (range > 0 && Racing != 0 && !Finished.Contains(slot.Slot))
            {
                float2 v = new float2(math.sin(aimYaw + bestOffset), math.cos(aimYaw + bestOffset)) * math.max(target, 10);
                float nearest = float.MaxValue;
                for (int k = 0; k < near.Length; k++)
                {
                    var o = near[k];
                    if (o.Kind == ObstacleKind.Rival) continue;
                    float2 rel = o.Position - pos; float d = math.length(rel);
                    if (d < (o.Kind == ObstacleKind.Mine ? MineShootMin : 25) || d > range || d >= nearest) continue;
                    float2 w = v - o.Velocity;
                    float t = math.clamp(math.dot(rel, w) / math.max(1e-3f, math.lengthsq(w)), 0, Horizon);
                    if (math.length(rel - w * t) >= o.Radius) continue;                        // it passes clear of this one
                    if (LineBlocked(near, k, pos, rel, d)) continue;
                    // in a drift the nose (and fixed guns) points off the way it goes: straighten up to take the shot
                    if (math.dot(math.normalizesafe(rel), forward) < math.cos(MineShootArc)) { lineUp = true; continue; }
                    nearest = d; gunAt = o.Position; shoot = true;
                }
                uint burst = math.hash(new uint2(pilot.Seed, Tick / 45));
                if (!shoot && (burst & 255) / 255f < pilot.Aggression)
                    for (int k = 0; k < near.Length; k++)
                    {
                        var o = near[k];
                        if (o.Kind != ObstacleKind.Rival) continue;
                        float2 rel = o.Position - pos; float d = math.length(rel);
                        if (d < 30 || d > range || d >= nearest) continue;
                        if (math.dot(math.normalizesafe(rel), forward) < math.cos(RivalShootArc) || LineBlocked(near, k, pos, rel, d)) continue;
                        nearest = d; gunAt = o.Position; shoot = true;
                    }
            }
            float steerYaw = aimYaw + bestOffset;
            float2 dir = new float2(math.sin(steerYaw), math.cos(steerYaw));
            // Threat ahead on the best heading too: ease off, and never arrive at structure faster than it can stop. Not for mines:
            // a fuse burns 0.5 s, so a fast ship is clear of the blast when it goes.
            if (bestThreat > 1) target *= math.saturate(1.1f - (bestThreat - 1) * .12f);
            if (bestClear < brakeReach) target = math.min(target, math.max(12, math.sqrt(2 * brake * math.max(0, bestClear - 8)) + 10));
            // A body it cannot pass on that heading (a slower rival with no room round it, a wreck, a mine it has not shot yet): no faster
            // than it can still brake to that body's own speed before it gets there. Left to the casts it saw a ship 8 m ahead and rammed
            // it at 60 m/s closing, and hit mine casings off a boost strip at 200 m/s.
            float2 bestU = new float2(math.sin(aimYaw + bestOffset), math.cos(aimYaw + bestOffset));
            for (int k = 0; k < near.Length; k++)
            {
                var o = near[k];
                float2 rel = o.Position - pos; float ahead = math.dot(rel, bestU);
                if (ahead <= 0) continue;
                float2 w = bestU * math.max(target, 10) - o.Velocity;
                float t = math.clamp(math.dot(rel, w) / math.max(1e-3f, math.lengthsq(w)), 0, Horizon);
                if (math.length(rel - w * t) >= o.Radius) continue;
                float theirs = math.max(0, math.dot(o.Velocity, bestU));
                target = math.min(target, math.max(12, theirs + math.sqrt(2 * brake * math.max(0, ahead - o.Radius))));
            }
            target = math.max(target, 12);

            // Stuck: no progress along the line for 3 s (wedged, spinning on a fence, crawling the wrong way) — back off and turn,
            // try again, and put it back at its last gate if that fails.
            pilot.Off = offLine - (lineHalf - edgeReach); pilot.Wide = math.max(pilot.Wide, pilot.Off);
            // (a jump back along the line — put back at a gate, or a new line — marks progress afresh)
            if (pilot.Index >= pilot.ProgressIndex + 3 || pilot.Index < pilot.ProgressIndex - 10 || pilot.ProgressTick == 0) { pilot.ProgressIndex = pilot.Index; pilot.ProgressTick = Tick; }
            uint slow = Tick - pilot.ProgressTick;
            bool reverse = slow >= 180 && slow < 250 || slow >= 400 && slow < 470;
            // Outside the fence line (through a downed span, or flung over it) for 3 s: the field holds it out, so reset too.
            if (offLine <= lineHalf + 12) pilot.OutsideSince = 0; else if (pilot.OutsideSince == 0) pilot.OutsideSince = Tick;
            bool outside = pilot.OutsideSince != 0 && Tick - pilot.OutsideSince > 180;
            if (slow > 660 || outside || offLine > lineHalf * 2 + 400) { pilot.WantsReset = 1; pilot.ProgressTick = Tick; pilot.OutsideSince = 0; }

            // Command: the wanted velocity, flown by the shared pilot (drifting where the strafe thrusters alone cannot turn it), with the
            // curve of its line just ahead as the pull to hold.
            float2 want = dir * target;
            float curvature = Curvature(line, pilot.Index, math.clamp(speed * .4f, 10, 80));
            bool boost = target > baseCap * 1.02f && speed < target - 5 && math.abs(pilot.Nose) < .1f && bestThreat < .5f && !reverse;
            float nose = pilot.Nose;
            var cmd = PilotControl.Fly(envelope, flight, pose.Rotation, vel, want, curvature, boost ? boostCap : baseCap, ref nose,
                lineUp && !shoot ? LineUpNose : PilotControl.MaxNose);
            pilot.Nose = nose;
            float error = Planar.WrapAngle(steerYaw + nose - yaw);
            if (boost) cmd.Flags |= ShipCommandFlags.Boost;
            // Well over its speed with the nose about right: Brake, the only way to shed a push in time (it cannot turn meanwhile).
            if (speed > target + 12 && math.abs(error) < .12f && pilot.Off < 0) cmd.Flags = ShipCommandFlags.Brake;
            if (reverse) { cmd.Thrust = new float2(0, -1); cmd.Turn = math.clamp(-error * 2, -1, 1); cmd.Flags = 0; }
            if (shoot)
            {
                float2 at = gunAt + Origin;
                cmd.AimWorld = new float3(at.x, 0, at.y); cmd.AimValid = 1; cmd.FireGroups = uint.MaxValue;
            }
            command = cmd;
            pilot.TargetSpeed = target; pilot.Threat = bestThreat + bestMines;
        }

        /// <summary>Signed curvature of the line `ahead` metres on from segment i (1/m, + = turning right).</summary>
        static float Curvature(in DynamicBuffer<RacerLinePoint> line, int i, float ahead)
        {
            int k = i; float walked = 0;
            while (k + 1 < line.Length && walked < ahead) { walked += math.distance(line[k].Position, line[k + 1].Position); k++; }
            int a = math.max(0, k - 3), c = math.min(line.Length - 1, k + 3);
            float2 ab = line[k].Position - line[a].Position, bc = line[c].Position - line[k].Position;
            float cross = ab.x * bc.y - ab.y * bc.x;   // < 0 turning right (yaw grows)
            return -math.sign(cross) / math.max(1, line[k].Radius);
        }

        bool OnStrip(float2 p, float radius)
        {
            for (int i = 0; i < Boosts.Length; i++)
            {
                var b = Boosts[i];
                float2 d = p - b.Center, f = new float2(math.sin(b.Yaw), math.cos(b.Yaw)), r = new float2(f.y, -f.x);
                if (math.abs(math.dot(d, r)) <= b.HalfExtents.x + radius && math.abs(math.dot(d, f)) <= b.HalfExtents.y) return true;
            }
            return false;
        }

        static float2 PointAlong(in DynamicBuffer<RacerLinePoint> line, int index, float t, float distance, out float2 direction, out float half)
        {
            float2 at = math.lerp(line[index].Position, line[index + 1].Position, t);
            direction = math.normalizesafe(line[index + 1].Position - line[index].Position, new float2(0, 1)); half = line[index].HalfWidth;
            for (int i = index + 1; i < line.Length; i++)
            {
                float step = math.distance(at, line[i].Position);
                direction = math.normalizesafe(line[i].Position - line[i - 1].Position, direction); half = line[i].HalfWidth;
                if (step >= distance) return at + (line[i].Position - at) * (distance / math.max(1e-4f, step));
                distance -= step; at = line[i].Position;
            }
            return at;
        }

        static float LineDistance(in DynamicBuffer<RacerLinePoint> line, int index, float2 p, out float half)
        {
            float best = float.MaxValue; half = line[index].HalfWidth;
            for (int i = index; i + 1 < line.Length && i <= index + 30; i++)
            {
                float d = RacerPilotSystem.SegmentDistanceSq(p, line[i].Position, line[i + 1].Position, out _);
                if (d < best) { best = d; half = line[i].HalfWidth; }
            }
            return math.sqrt(best);
        }
    }

    /// <summary>The nearest cast hit that is not the caster's own grid.</summary>
    internal struct NavHitCollector : ICollector<ColliderCastHit>
    {
        readonly Entity self;
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }
        public int NumHits { get; private set; }
        public float Fraction => MaxFraction;
        internal NavHitCollector(Entity self, float maxFraction) { this.self = self; MaxFraction = maxFraction; NumHits = 0; }
        public bool AddHit(ColliderCastHit hit)
        {
            if (hit.Entity == self || hit.Fraction > MaxFraction) return false;
            MaxFraction = hit.Fraction; NumHits = 1; return true;
        }
    }
}
