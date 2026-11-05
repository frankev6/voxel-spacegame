using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Simulation.Flight
{
    /// <summary>ADR-0122: sole writer of <see cref="Cruise"/>. Runs last among the command writers: it reads the Cruise edge, decides the
    /// phase from clearance (the spatial hash), mass lock and damage, steps the cruise speed toward its cap, and holds the ship's
    /// weapons while spooling or cruising. Ships in Off with no press cost one branch.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ShipCommandGroup), OrderLast = true)]
    public partial struct CruiseSystem : ISystem
    {
        static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker(ProfilerCategory.Scripts, "CruiseSystem.Schedule");
        EntityQuery zones;
        ComponentLookup<Allegiance> allegiances;
        ComponentLookup<GridDamageStats> damage;
        ComponentLookup<Flight3D> flight3D;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<FlightConfig>();
            state.RequireForUpdate<FactionRelations>();
            if (!SystemAPI.HasSingleton<CruiseConfig>()) state.EntityManager.CreateSingleton(CruiseConfig.Default);
            zones = SystemAPI.QueryBuilder().WithAll<CruiseZone, LocalTransform>().Build();
            allegiances = state.GetComponentLookup<Allegiance>(true);
            damage = state.GetComponentLookup<GridDamageStats>(true);
            flight3D = state.GetComponentLookup<Flight3D>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = ScheduleMarker.Auto();
            allegiances.Update(ref state); damage.Update(ref state); flight3D.Update(ref state);
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            var zoneData = zones.ToComponentDataListAsync<CruiseZone>(state.WorldUpdateAllocator, state.Dependency, out var a);
            var zonePoses = zones.ToComponentDataListAsync<LocalTransform>(state.WorldUpdateAllocator, state.Dependency, out var b);
            var job = new CruiseJob
            {
                Config = SystemAPI.GetSingleton<CruiseConfig>(), ForwardCap = SystemAPI.GetSingleton<FlightConfig>().MaxForwardSpeed,
                Hash = hash.AsReadOnly(), Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Allegiances = allegiances, Damage = damage, Flight3D = flight3D, Zones = zoneData, ZonePoses = zonePoses,
                Tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0
            };
            var handle = job.ScheduleParallel(hash.BeginRead(Unity.Jobs.JobHandle.CombineDependencies(state.Dependency, a, b)));
            hash.EndRead(handle);
            state.Dependency = handle;
        }

        [BurstCompile]
        [WithNone(typeof(Hulk))]
        partial struct CruiseJob : IJobEntity
        {
            public CruiseConfig Config;
            public float ForwardCap;
            [ReadOnly] public SpatialHashQueryData Hash;
            [ReadOnly] public FactionRelations Relations;
            [ReadOnly] public ComponentLookup<Allegiance> Allegiances;
            [ReadOnly] public ComponentLookup<GridDamageStats> Damage;
            [ReadOnly] public ComponentLookup<Flight3D> Flight3D;   // ADR-0123: no cruise in experimental 3D flight
            [ReadOnly] public NativeList<CruiseZone> Zones;
            [ReadOnly] public NativeList<LocalTransform> ZonePoses;
            public uint Tick;

            void Execute(Entity entity, ref Cruise cruise, ref ShipCommand command, in LocalTransform pose, in PhysicsVelocity velocity)
            {
                bool pressed = (command.Edges & ShipCommandEdges.Cruise) != 0;
                cruise.Exit = 0;
                if (cruise.Phase == CruisePhase.Off && !pressed) return;

                // Clearance and mass lock from last tick's proxies (planets lie below the plane and are not obstacles).
                int self = Hash.FindIndex(entity);
                float3 centre = self >= 0 ? Hash.Proxies[self].Center : pose.Position;
                float radius = self >= 0 ? Hash.Proxies[self].HullRadius : 0;
                var allegiance = Allegiances.HasComponent(entity) ? Allegiances[entity] : Allegiance.None;
                float clearance = float.MaxValue, lockDistance = 0;
                for (int i = 0; i < Hash.Proxies.Length; i++)
                {
                    if (i == self) continue;
                    var proxy = Hash.Proxies[i];
                    if (proxy.Kind == ProxyKind.Celestial) continue;
                    float gap = math.distance(proxy.Center.xz, centre.xz) - proxy.HullRadius - radius;
                    clearance = math.min(clearance, gap);
                    if (proxy.Kind == ProxyKind.Ship && gap < Config.MassLockRange && Relations.IsHostile(allegiance, proxy.Allegiance)
                        && (lockDistance == 0 || gap < lockDistance))
                        lockDistance = math.max(1, gap);
                }
                float zoneCap = float.MaxValue;
                for (int i = 0; i < Zones.Length; i++) zoneCap = math.min(zoneCap, CruiseMath.ZoneCap(pose.Position.xz, ZonePoses[i].Position.xz, Zones[i]));
                bool hit = Damage.HasComponent(entity) && Damage[entity].LastDamageTick != 0 && Tick - Damage[entity].LastDamageTick < (uint)Config.DamageLockTicks;
                cruise.Clearance = clearance; cruise.LockDistance = lockDistance;
                cruise.Cap = CruiseMath.Cap(Config, ForwardCap, clearance, zoneCap);
                bool flying3D = Flight3D.HasComponent(entity) && Flight3D.IsComponentEnabled(entity);
                var why = flying3D ? CruiseBlock.Flight3D : lockDistance > 0 ? CruiseBlock.MassLocked : hit ? CruiseBlock.Hit : clearance < Config.SlowRadius ? CruiseBlock.TooClose : CruiseBlock.None;
                bool firing = command.FireGroups != 0;
                bool brake = (command.Flags & ShipCommandFlags.Brake) != 0 || command.Thrust.y < -.5f;

                switch (cruise.Phase)
                {
                    case CruisePhase.Off:
                        if (why == CruiseBlock.None) { cruise.Phase = CruisePhase.Spooling; cruise.PhaseTick = Tick; cruise.Block = CruiseBlock.None; }
                        else Refuse(ref cruise, why);
                        break;
                    case CruisePhase.Spooling:
                        if (pressed || firing || brake) Stop(ref cruise, CruiseBlock.Cancelled, false);
                        else if (why != CruiseBlock.None) Stop(ref cruise, why, false);
                        else if (Tick - cruise.PhaseTick >= (uint)Config.SpoolTicks)
                        {
                            cruise.Phase = CruisePhase.On; cruise.PhaseTick = Tick;
                            float forward = math.dot(velocity.Linear.xz, math.forward(pose.Rotation).xz);
                            cruise.Speed = math.min(cruise.Cap, math.max(forward, ForwardCap));
                        }
                        break;
                    case CruisePhase.On:
                        if (pressed || brake) Stop(ref cruise, CruiseBlock.Cancelled, true);
                        else if (why == CruiseBlock.MassLocked || why == CruiseBlock.Hit) Stop(ref cruise, why, true);
                        else if (cruise.Cap <= ForwardCap + .5f) Stop(ref cruise, CruiseBlock.Arrived, true);
                        else cruise.Speed = CruiseMath.Step(Config, cruise.Speed, cruise.Cap, SimTime.FixedTimestep);
                        break;
                }
                if (cruise.Phase != CruisePhase.Off) command.FireGroups = 0;   // weapons hold while spooling or cruising
            }

            void Refuse(ref Cruise cruise, CruiseBlock why) { cruise.Block = why; cruise.BlockTick = Tick; }

            void Stop(ref Cruise cruise, CruiseBlock why, bool wasOn)
            {
                cruise.Phase = CruisePhase.Off; cruise.PhaseTick = Tick; cruise.Speed = 0;
                cruise.Exit = (byte)(wasOn ? 1 : 0);
                Refuse(ref cruise, why);
            }
        }
    }
}
