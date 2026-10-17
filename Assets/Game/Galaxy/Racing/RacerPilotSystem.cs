using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Hazards;
using Game.Simulation.Spatial;
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
        public struct Ability { public float Speed, Lateral, Brake, YawCap; }

        public static Ability AbilityOf(in PropulsionStats stats, in FlightConfig config, float skill, float speed, float yawCap) => new Ability
        {
            Speed = speed, YawCap = yawCap,
            Lateral = math.max(4, math.max(stats.StrafeAccel, stats.ForwardAccel * .5f)) * (.8f + .15f * math.saturate(skill)),
            // the engines, but no faster than a push (a boost strip's) bleeds off under Brake (ADR-0118 carry)
            Brake = math.min(math.max(3, stats.ReverseAccel), config.CarryBrakeDecel > 0 ? config.CarryBrakeDecel : 1e6f) * .85f,
        };

        /// <summary>The fastest it may take a turn of radius r.</summary>
        public static float CornerSpeed(in Ability a, float r) => math.min(math.sqrt(a.Lateral * r), a.YawCap * .85f * r);

        /// <summary>Whether a racer at its speed now can make the corners of a leg's first FeasibleSpan metres, braking all it can except
        /// where a boost strip pushes it (it cannot brake against one). A shortcut that leaves a strip in a tight S is not for a racer
        /// already doing 150 m/s on it.</summary>
        public static bool Feasible(DynamicBuffer<RaceLegPoint> points, in RaceLeg leg, in Ability a) => Excess(points, leg, a) <= 1;

        /// <summary>How far over its corner limits (with 15% grace) a racer would be on a leg's first FeasibleSpan metres: ≤ 1 makeable.</summary>
        public static float Excess(DynamicBuffer<RaceLegPoint> points, in RaceLeg leg, in Ability a)
        {
            float v = a.Speed, walked = 0, worst = 0;
            for (int i = 1; i < leg.Count && walked < FeasibleSpan; i++)
            {
                var p = points[leg.Start + i];
                float ds = math.distance(points[leg.Start + i - 1].Position, p.Position); walked += ds;
                v = p.PushSpeed > 0 && v < p.PushSpeed ? math.min(p.PushSpeed, math.sqrt(v * v + 2 * p.PushAccel * ds)) : math.sqrt(math.max(0, v * v - 2 * a.Brake * ds));
                float r = Radius(points[leg.Start + math.max(0, i - 3)].Position, p.Position, points[leg.Start + math.min(leg.Count - 1, i + 3)].Position);
                worst = math.max(worst, v / (CornerSpeed(a, r) * 1.15f + 5));
            }
            return worst;
        }

        public static float Radius(float2 a, float2 b, float2 c)
        {
            float2 ab = b - a, bc = c - b;
            float s = math.length(ab) + math.length(bc), turn = 0;
            if (math.lengthsq(ab) > 1e-4f && math.lengthsq(bc) > 1e-4f) turn = math.acos(math.clamp(math.dot(math.normalize(ab), math.normalize(bc)), -1, 1));
            return turn > 1e-3f ? s / turn : 1e5f;
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
                    line.Add(new RacerLinePoint { Position = p.Position, HalfWidth = p.HalfWidth, PushSpeed = p.PushSpeed });
                }
                at = l.To;
            }
            // Turn radius at each point from the heading change across ±3 points.
            for (int i = 0; i < line.Count; i++)
            {
                var p = line[i]; p.Radius = Radius(line[math.max(0, i - 3)].Position, p.Position, line[math.min(line.Count - 1, i + 3)].Position); line[i] = p;
            }
            return line.Count > 1 ? at : -1;
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
                        var ability = RacerPlan.AbilityOf(em.HasComponent<PropulsionStats>(ship) ? em.GetComponentData<PropulsionStats>(ship) : default, config, pilot.Skill,
                            speed, em.HasComponent<FlightState>(ship) ? em.GetComponentData<FlightState>(ship).YawRateCap : 2);
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
            bool done(RacerFlags f) => (f & RacerFlags.Finished) != 0;
            var finished = new NativeHashSet<byte>(4, state.WorldUpdateAllocator);
            foreach (var r in em.GetBuffer<RacerProgress>(race)) if (done(r.Flags)) finished.Add(r.Slot);
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            var job = new RacerSteerJob
            {
                Origin = origin, Tick = tick, Racing = (byte)(rs.Phase == RacePhase.Racing ? 1 : 0), Config = SystemAPI.GetSingleton<FlightConfig>(),
                Hash = hash.AsReadOnly(), Collision = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld, Finished = finished,
                Flights = flights, Propulsion = propulsion, Grids = grids, Velocities = velocities, Holds = holds, Mines = mines, Spawning = spawning,
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
            em.AddComponentData(ship, new RacerPilot { Skill = skill, Seed = seed, LineFrom = -2, Lane = lane });
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
        [ReadOnly] public ComponentLookup<FlightState> Flights;
        [ReadOnly] public ComponentLookup<PropulsionStats> Propulsion;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<RaceHold> Holds;
        [ReadOnly] public ComponentLookup<MineFuse> Mines;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;

        // Headings tried around the line target (rad): straight first, then wider either side.
        static readonly float[] Offsets = { 0, .1f, -.1f, .21f, -.21f, .35f, -.35f, .52f, -.52f, .73f, -.73f, .98f, -.98f, 1.3f, -1.3f };
        public const float Horizon = 3;          // s: how far ahead other bodies are predicted
        public const float Margin = 5;           // m kept from anything, on top of both radii
        public const float CorridorMargin = 10;  // m kept inside the route's edge (the field walls)
        public const float MineShootMin = 70, MineShootMax = 380;   // m: outside a mine's 28 m blast, inside gun range
        public const float MineShootArc = .3f;                     // rad off the nose (fixed guns converge on the aim point)

        struct Obstacle { public float2 Position, Velocity; public float Radius; public bool Mine; }

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
            float edgeReach = math.max(radius, ZoneSystem.WallRadiusFraction * hull + 3) + CorridorMargin;

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
            var ability = RacerPlan.AbilityOf(stats, Config, skill, speed, flight.YawRateCap);
            float brake = ability.Brake;
            bool boosting = (command.Flags & ShipCommandFlags.Boost) != 0;
            float baseCap = boosting ? flight.ForwardCap / math.max(1, Config.BoostSpeedMultiplier) : flight.ForwardCap;
            float boostCap = baseCap * math.max(1, Config.BoostSpeedMultiplier);
            float yawCap = flight.YawRateCap;
            float target = boostCap, reach = speed * speed / (2 * brake) + 80, along = -bestT * math.distance(line[pilot.Index].Position, line[pilot.Index + 1].Position), room = along;
            for (int i = pilot.Index; i < line.Length && along < reach + 600; i++)
            {
                if (i > pilot.Index)
                {
                    float ds = math.distance(line[i - 1].Position, line[i].Position);
                    along += ds; if (line[i].PushSpeed <= 0) room += ds;
                }
                if (room > reach) break;
                float corner = RacerPlan.CornerSpeed(ability, line[i].Radius);
                target = math.min(target, math.sqrt(corner * corner + 2 * brake * math.max(0, room)));
            }
            if (pilot.Index + 2 >= line.Length) target = math.min(target, 20);   // the end of its line (it is rebuilt at the next gate)
            target *= .86f + .14f * skill;
            if (Racing == 0 || Finished.Contains(slot.Slot)) target = math.min(target, baseCap * .5f);

            // The point it steers for, ahead along the line, in its own lane. Nearer in a tight corner: a chord to a far point would
            // cut across the inside fence (its sagitta L²/8R kept within half the free width).
            float look = math.clamp(25 + speed * .8f, 40, 180), tightest = 1e5f, walked = 0;
            for (int i = pilot.Index + 1; i < line.Length && walked < look; i++) { walked += math.distance(line[i - 1].Position, line[i].Position); tightest = math.min(tightest, line[i].Radius); }
            float free = math.max(6, lineHalf - edgeReach);
            look = math.min(look, math.max(35, math.sqrt(8 * tightest * free * .5f)));
            float2 aim = PointAlong(line, pilot.Index, bestT, look, out float2 aimDir, out float aimHalf);
            aim += new float2(aimDir.y, -aimDir.x) * pilot.Lane * math.max(0, aimHalf - edgeReach);
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
                near.Add(new Obstacle { Position = proxy.Center.xz - Origin, Velocity = proxy.Velocity.xz, Radius = r, Mine = mine });
            }

            // Score each heading: how far it turns from the line, leaving the route, bodies it would meet, structure it would hit.
            float bestCost = float.MaxValue, bestOffset = 0, bestThreat = 0, bestMines = 0, bestClear = float.MaxValue;
            float castLength = math.clamp(speed * 1.4f + 30, 50, 280), castRadius = math.clamp(halfWidth * .9f, 2, 14);
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
            // A mine it cannot pass clear of, ahead in its guns' arc and beyond the blast: shoot it (knocked aside, or set off safely).
            float2 gunAt = default; bool shoot = false; float nearestMine = float.MaxValue;
            {
                float2 v = new float2(math.sin(aimYaw + bestOffset), math.cos(aimYaw + bestOffset)) * math.max(target, 10);
                for (int k = 0; k < near.Length; k++)
                {
                    var o = near[k];
                    if (!o.Mine) continue;
                    float2 rel = o.Position - pos; float d = math.length(rel);
                    if (d < MineShootMin || d > MineShootMax || d >= nearestMine) continue;
                    float2 w = v - o.Velocity;
                    float t = math.clamp(math.dot(rel, w) / math.max(1e-3f, math.lengthsq(w)), 0, Horizon);
                    if (math.length(rel - w * t) >= o.Radius) continue;                        // it passes clear of this one
                    if (math.dot(math.normalizesafe(rel), forward) < math.cos(MineShootArc)) continue;
                    bool blocked = false;                                                  // never through another ship
                    for (int j = 0; j < near.Length && !blocked; j++)
                    {
                        if (near[j].Mine) continue;
                        float2 r2 = near[j].Position - pos;
                        blocked = math.length(r2) < d && math.dot(math.normalizesafe(r2), math.normalizesafe(rel)) > math.cos(.15f);
                    }
                    if (blocked) continue;
                    nearestMine = d; gunAt = o.Position; shoot = true;
                }
            }
            float steerYaw = aimYaw + bestOffset;
            float2 dir = new float2(math.sin(steerYaw), math.cos(steerYaw));
            // Threat ahead on the best heading too: ease off, and never arrive at structure faster than it can stop. Not for mines:
            // a fuse burns 0.5 s, so a fast ship is clear of the blast when it goes.
            if (bestThreat > 1) target *= math.saturate(1.1f - (bestThreat - 1) * .12f);
            if (bestClear < float.MaxValue) target = math.min(target, math.max(12, math.sqrt(2 * brake * math.max(0, bestClear - 8)) + 10));
            target = math.max(target, 12);

            // Stuck: no progress along the line for 3 s (wedged, spinning on a fence, crawling the wrong way) — back off and turn,
            // try again, and put it back at its last gate if that fails.
            pilot.Off = offLine - (lineHalf - edgeReach); pilot.Wide = math.max(pilot.Wide, pilot.Off);
            if (pilot.Index >= pilot.ProgressIndex + 3 || pilot.ProgressTick == 0) { pilot.ProgressIndex = math.max(pilot.ProgressIndex, pilot.Index); pilot.ProgressTick = Tick; }
            uint slow = Tick - pilot.ProgressTick;
            bool reverse = slow >= 180 && slow < 250 || slow >= 400 && slow < 470;
            // Outside the fence line (through a downed span, or flung over it) for 3 s: the field holds it out, so reset too.
            if (offLine <= lineHalf + 12) pilot.OutsideSince = 0; else if (pilot.OutsideSince == 0) pilot.OutsideSince = Tick;
            bool outside = pilot.OutsideSince != 0 && Tick - pilot.OutsideSince > 180;
            if (slow > 660 || outside || offLine > lineHalf * 2 + 400) { pilot.WantsReset = 1; pilot.ProgressTick = Tick; pilot.OutsideSince = 0; }

            // Command: the velocity it wants, with the nose led into the turn so the assist can pull the drift round.
            float2 want = dir * target;
            // Lead only across the wanted direction: the along part (braking) is the thrusters' job, and leading into it would swing
            // the nose round and slide the ship sideways.
            float2 slip = want - vel; slip -= dir * math.dot(slip, dir);
            float2 lead = want + slip * .6f;
            float faceYaw = math.lengthsq(lead) > 1 ? math.atan2(lead.x, lead.y) : steerYaw;
            float error = Planar.WrapAngle(faceYaw - yaw);
            var cmd = default(ShipCommand);
            cmd.Turn = math.clamp(error * 3 / yawCap, -1, 1);
            // The assist adds a push's carry to whatever is commanded (ADR-0118), so command the difference: after a boost strip
            // that is reverse thrust, and the engines brake instead of holding the strip's speed.
            float2 local = FlightMath.ToLocalPlanar(new float3(want.x, 0, want.y), pose.Rotation) - flight.Carry;
            bool boost = target > baseCap * 1.02f && speed < target - 5 && math.abs(error) < .15f && bestThreat < .5f && !reverse;
            float cap = boost ? boostCap : baseCap;
            cmd.Thrust.x = math.clamp(local.x / math.max(1, flight.StrafeCap), -1, 1);
            cmd.Thrust.y = local.y >= 0 ? math.clamp(local.y / math.max(1, cap), 0, 1) : math.clamp(local.y / math.max(1, flight.ReverseCap), -1, 0);
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
