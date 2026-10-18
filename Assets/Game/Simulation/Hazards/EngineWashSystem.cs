using Game.Core.Net;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Simulation.Flight;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Hazards
{
    /// <summary>HAZ-017 (ADR-0119 decision 5): engine wash, derived from thrusters, never authored. Behind every firing thruster is a cone
    /// whose reach and push grow with MaxForce × Intensity. Bodies in it (never the ship that owns the thruster) are pushed along the
    /// exhaust through DamageStreams.Impulses (ProducerKind.Hazard), on every machine. Bodies close to the nozzle also take heat damage,
    /// decided on the authority only.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(ImpulseApplySystem))]
    public partial struct EngineWashSystem : ISystem
    {
        /// <summary>Reach (m) of a 10 kN thruster at full intensity; it grows with the square root of the force.</summary>
        public const float ReachPer10kN = 12;
        /// <summary>Fraction of the thrust that reaches a body at the nozzle; it falls off linearly to zero at the reach.</summary>
        public const float PushFraction = .3f;
        /// <summary>Cone half-angle and the nozzle radius of a 10 kN thruster (m), which grows like the reach.</summary>
        public const float HalfAngle = .26f, NozzlePer10kN = .5f;
        /// <summary>Heat: HP per second per N of thrust at the nozzle, inside the first HeatReach of the reach; zero beyond it.</summary>
        public const float HeatPerNewton = 1e-4f, HeatReach = .3f;
        const float MinIntensity = .05f;
        const uint SequenceBase = 0x80000000u;   // above MineSystem's Hazard impulse keys (entity indices)

        EntityQuery ships;
        ComponentLookup<LocalTransform> transforms;
        ComponentLookup<PhysicsMass> masses;
        ComponentLookup<ThrusterState> states;
        ComponentLookup<VoxelGrid> grids;
        BufferLookup<FlightThruster> thrusters;

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            ships = SystemAPI.QueryBuilder().WithAll<FlightThruster, LocalTransform, PhysicsMass>().Build();
            transforms = state.GetComponentLookup<LocalTransform>(true); masses = state.GetComponentLookup<PhysicsMass>(true);
            states = state.GetComponentLookup<ThrusterState>(true); grids = state.GetComponentLookup<VoxelGrid>(true);
            thrusters = state.GetBufferLookup<FlightThruster>(true);
            state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<SpatialHash>(); state.RequireForUpdate(ships);
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            transforms.Update(ref state); masses.Update(ref state); states.Update(ref state); grids.Update(ref state); thrusters.Update(ref state);
            bool authority = !(SystemAPI.TryGetSingleton<SimRole>(out var role) && role.IsGuest);
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var hash = SystemAPI.GetSingleton<SpatialHash>();
            var shipList = ships.ToEntityListAsync(state.WorldUpdateAllocator, out var gathered);
            var ready = hash.BeginRead(Unity.Jobs.JobHandle.CombineDependencies(state.Dependency, gathered));
            var job = new WashJob
            {
                Ships = shipList, Hash = hash.AsReadOnly(),
                Transforms = transforms, Masses = masses, States = states, Grids = grids, Thrusters = thrusters,
                Authority = authority, Dt = Game.Core.SimTime.FixedTimestep,
                Impulses = streams.Impulses.BeginWrite(ref state, ProducerKind.Hazard, 1),
                Damage = streams.Damage.BeginWrite(ref state, ProducerKind.Hazard, 1),
            };
            var handle = job.Schedule(ready);
            streams.Impulses.EndWrite(handle); streams.Damage.EndWrite(handle); hash.EndRead(handle);
            state.Dependency = handle;
        }

        /// <summary>Reach (m) of a thruster of <paramref name="force"/> N at <paramref name="intensity"/>.</summary>
        public static float Reach(float force, float intensity) => ReachPer10kN * math.sqrt(math.max(0, force * intensity) / 1e4f);

        [BurstCompile] struct WashJob : Unity.Jobs.IJob
        {
            [ReadOnly] public NativeList<Entity> Ships;
            [ReadOnly] public SpatialHashQueryData Hash;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
            [ReadOnly] public ComponentLookup<ThrusterState> States;
            [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
            [ReadOnly] public BufferLookup<FlightThruster> Thrusters;
            public bool Authority; public float Dt;
            public NativeStream.Writer Impulses, Damage;

            public void Execute()
            {
                Impulses.BeginForEachIndex(0); Damage.BeginForEachIndex(0);
                uint sequence = SequenceBase;
                var candidates = new FixedList512Bytes<int>();
                for (int s = 0; s < Ships.Length; s++)
                {
                    var owner = Ships[s]; var pose = Transforms[owner];
                    var buffer = Thrusters[owner];
                    for (int k = 0; k < buffer.Length; k++)
                    {
                        var thruster = buffer[k];
                        float intensity = States.HasComponent(thruster.Thruster) ? States[thruster.Thruster].Intensity : thruster.Throttle;
                        if (intensity < MinIntensity || !(thruster.MaxForce > 0)) continue;
                        float reach = Reach(thruster.MaxForce, intensity), scale = reach / ReachPer10kN;
                        float3 apex = pose.TransformPoint(thruster.PositionLocal), axis = -math.rotate(pose.Rotation, thruster.ForceDirLocal);
                        float nozzle = NozzlePer10kN * scale, spread = math.tan(HalfAngle);
                        candidates.Clear();
                        Hash.QuerySphere(apex + axis * (reach * .5f), reach * .5f + nozzle + reach * spread, ref candidates);
                        for (int c = 0; c < candidates.Length; c++)
                        {
                            var proxy = Hash.Get(candidates[c]); var body = proxy.Entity;
                            if (body == owner || proxy.Kind == ProxyKind.Celestial || !Masses.HasComponent(body) || !(Masses[body].InverseMass > 0)) continue;
                            float3 d = proxy.Center - apex; float along = math.dot(d, axis);
                            if (along <= 0 || along - proxy.HullRadius >= reach) continue;
                            // Measured to the body's near side: a long hull is washed from its nearest end.
                            float t = math.clamp(along - proxy.HullRadius, 0, reach), side = math.length(d - axis * along);
                            if (side - proxy.HullRadius > nozzle + t * spread) continue;
                            float falloff = 1 - t / reach;
                            float force = PushFraction * thruster.MaxForce * intensity * falloff;
                            float3 point = apex + axis * t;
                            Impulses.Write(new ImpulseRequest { Body = body, WorldImpulse = axis * (force * Dt), WorldPoint = point,
                                Flags = ImpulseRequestFlags.AtCenterOfMass, Producer = ProducerKind.Hazard, Sequence = sequence++ });
                            if (!Authority || t > reach * HeatReach || !Grids.HasComponent(body) || !Transforms.HasComponent(body)) continue;
                            float heat = HeatPerNewton * thruster.MaxForce * intensity * (1 - t / (reach * HeatReach)) * Dt;
                            if (heat <= 0) continue;
                            Damage.Write(new DamageEvent { Grid = body, LocalPoint = Transforms[body].InverseTransformPoint(point), Amount = heat,
                                Radius = math.max(1, nozzle), Type = DamageType.Energy, Source = owner, Instigator = owner,
                                Producer = ProducerKind.Hazard, Sequence = sequence++ });
                        }
                    }
                }
                Impulses.EndForEachIndex(); Damage.EndForEachIndex();
            }
        }
    }
}
