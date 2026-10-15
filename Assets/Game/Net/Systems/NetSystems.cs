using Game.Core.Components;
using Game.Core.Systems;
using Game.Galaxy.Components;
using Game.Net.Protocol;
using Game.Net.Replication;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Grid;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;


namespace Game.Net.Systems
{
    /// <summary>Base for the co-op systems: disabled until Game.App's RoleSystemPolicy enables them for a live session (SPEC-90 §5),
    /// so solo play runs none of them.</summary>
    public abstract partial class NetSystemBase : SystemBase
    {
        protected CoopNet Net { get; private set; }
        public void Bind(CoopNet net) { Net = net; }
        public void Unbind() { Net = null; }
        protected override void OnCreate() { Enabled = false; }
    }

    /// <summary>Host, each fixed tick: gives each guest's ship the next command from that guest's queue, one per tick, as the guest's
    /// prediction applied them (SPEC-90 NET-006, NET-011). RemotePilotApplySystem turns it into ShipCommand.</summary>
    [UpdateInGroup(typeof(ShipCommandGroup)), UpdateBefore(typeof(RemotePilotApplySystem))]
    public partial class RemotePilotReceiveSystem : NetSystemBase
    {
        EntityQuery pilots;
        protected override void OnCreate()
        {
            base.OnCreate();
            pilots = GetEntityQuery(ComponentType.ReadOnly<RemotePilot>(), ComponentType.ReadWrite<RemotePilotInput>());
        }
        protected override void OnUpdate()
        {
            if (Net?.Host == null) return;
            var em = EntityManager;
            using var entities = pilots.ToEntityArray(Allocator.Temp);
            em.CompleteDependencyBeforeRW<RemotePilotInput>();
            foreach (var entity in entities)
            {
                byte slot = em.GetComponentData<RemotePilot>(entity).Slot;
                if (!Net.TryPopCommand(slot, out uint sequence, out var command)) continue;
                em.SetComponentData(entity, new RemotePilotInput { Command = command, Sequence = sequence });
            }
        }
    }

    /// <summary>Guest: applies the host's reliable stream (spawns, despawns, cell deltas, chunks, hash checks) before this frame's
    /// GridSpawnSystem, so replica spawns start in the same frame (SPEC-90 ReplicaSpawnSystem and ReplicaCellApplySystem).</summary>
    [UpdateInGroup(typeof(GameInitGroup)), UpdateBefore(typeof(GridSpawnSystem))]
    public partial class ReplicaApplySystem : NetSystemBase
    {
        protected override void OnCreate() { base.OnCreate(); RequireForUpdate<GameplayActive>(); }
        protected override void OnUpdate()
        {
            if (Net?.Guest == null) return;
            Net.Guest.Apply(EntityManager, Net.Registry, Net.Transport.LocalSlot);
        }
    }

    /// <summary>Guest: drives every replicated body 100 ms behind the host from the interpolation tracks, first in the fixed step,
    /// and adopts the host's frame origin (SPEC-90 ReplicaBodySyncSystem). Writer of replicated LocalTransform, PhysicsVelocity,
    /// ShipCommand (except the guest's own ship) and ShieldState on guests.</summary>
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(ShipCommandGroup))]
    [UpdateBefore(typeof(Game.Simulation.Weapons.WeaponAimSystem)), UpdateBefore(typeof(Game.Simulation.Flight.PropulsionSystem)), UpdateBefore(typeof(Game.Simulation.Power.PowerDemandGroup))]
    public partial class ReplicaBodySyncSystem : NetSystemBase
    {
        EntityQuery replicas;
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<GameplayActive>();
            replicas = GetEntityQuery(ComponentType.ReadWrite<Replicated>(), ComponentType.ReadWrite<LocalTransform>());
        }
        protected override void OnUpdate()
        {
            var guest = Net?.Guest;
            if (guest == null) return;
            // NET-011: this tick's command gets its own sequence; the host applies it for exactly one tick too.
            uint sequence = Net.RecordTickCommand(SystemAPI.TryGetSingleton<PlayerCommand>(out var input) ? input : default, guest.HostOrigin);
            if (!guest.HasOrigin) return;
            var em = EntityManager;
            em.CompleteAllTrackedJobs();
            double3 origin = guest.HostOrigin, previousOrigin = origin;
            if (SystemAPI.TryGetSingletonRW<FrameOrigin>(out var frame))
            {
                previousOrigin = frame.ValueRO.OriginInStarSystem;
                if (math.any(previousOrigin != origin)) frame.ValueRW.OriginInStarSystem = origin;
            }
            double tick = guest.RenderTick;
            using var entities = replicas.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var replica = em.GetComponentData<Replicated>(entity);
                if (!guest.TryGetTrack(replica.NetId, out var track) || !track.Sample(tick, NetProtocol.MaxExtrapolationTicks, out var s)) continue;
                if (replica.OwnedLocally != 0 && em.HasComponent<PhysicsMass>(entity) && em.GetComponentData<PhysicsMass>(entity).InverseMass > 0)
                    Predict(em, entity, guest, track, previousOrigin, origin, sequence);
                else
                {
                    var pose = em.GetComponentData<LocalTransform>(entity);
                    pose.Position = (float3)(s.Position - origin); pose.Rotation = math.normalizesafe(s.Rotation, quaternion.identity);
                    if (NetIds.IsChunk(replica.NetId)) pose.Scale = s.Scale;
                    em.SetComponentData(entity, pose);
                    if (em.HasComponent<PhysicsVelocity>(entity)) em.SetComponentData(entity, new PhysicsVelocity { Linear = s.Linear });
                    if (track.PlumeVersion != track.ShownVersion) ShowPlumes(em, entity, track);
                }
                if (s.HasCommand != 0 && replica.OwnedLocally == 0 && em.HasComponent<ShipCommand>(entity))
                {
                    var command = s.Command; command.AimWorld -= (float3)origin; command.Edges = ShipCommandEdges.None;
                    em.SetComponentData(entity, command);
                }
                if (s.HasWeapons != 0 && replica.OwnedLocally == 0)
                {
                    if (em.HasComponent<Game.Simulation.Weapons.WeaponControl>(entity))
                        em.SetComponentData(entity, new Game.Simulation.Weapons.WeaponControl { Autofire = s.Autofire, AimMode = (Game.Simulation.Weapons.WeaponAimMode)s.AimMode });
                    if (em.HasComponent<Game.Simulation.Weapons.ShipTarget>(entity))
                    {
                        var target = em.GetComponentData<Game.Simulation.Weapons.ShipTarget>(entity);
                        Entity resolved = s.Target != 0 && guest.TryGetEntity(s.Target, out var t) ? t : Entity.Null;
                        if (target.Value != resolved) { target.Value = resolved; em.SetComponentData(entity, target); }
                    }
                }
                if (s.HasShield != 0 && em.HasComponent<ShieldState>(entity))
                {
                    var shield = em.GetComponentData<ShieldState>(entity);
                    shield.Hp = s.ShieldHp; shield.MaxHp = s.ShieldMax; shield.Active = s.ShieldActive;
                    em.SetComponentData(entity, shield);
                }
                replica.LastSnapshotTick = track.NewestTick; em.SetComponentData(entity, replica);
            }
        }

        /// <summary>Thruster plumes of a kinematic replica from the newest snapshot: lights the listed thrusters and puts out the rest of
        /// those shown last time. ThrusterState feeds plumes and engine sound; on guests nothing else writes it for replicas.</summary>
        static void ShowPlumes(EntityManager em, Entity root, ReplicaTrack track)
        {
            track.ShownVersion = track.PlumeVersion;
            if (!em.HasBuffer<Game.Voxel.Model.BlockInstance>(root)) return;
            var instances = em.GetBuffer<Game.Voxel.Model.BlockInstance>(root, true);
            for (int i = 0; i < track.ShownCount; i++) SetPlume(em, instances, track.ShownPlumes[i], 0);
            for (int i = 0; i < track.PlumeCount; i++)
            {
                SetPlume(em, instances, track.PlumeInstances[i], ReplicaTrack.DecodePlume(track.PlumeLevels[i]));
                track.ShownPlumes[i] = track.PlumeInstances[i];
            }
            track.ShownCount = track.PlumeCount;
        }

        static void SetPlume(EntityManager em, DynamicBuffer<Game.Voxel.Model.BlockInstance> instances, int index, float intensity)
        {
            if (index >= instances.Length) return;
            var functional = instances[index].Functional;
            if (functional == Entity.Null || !em.Exists(functional) || !em.HasComponent<Game.Simulation.Flight.ThrusterState>(functional)) return;
            em.SetComponentData(functional, new Game.Simulation.Flight.ThrusterState { Intensity = intensity });
        }

        /// <summary>NET-011: the guest's own ship is simulated here from local input. Records the state the last tick produced under that
        /// tick's command sequence, corrects it toward the newest acknowledged host state, and labels this tick with the next command.</summary>
        void Predict(EntityManager em, Entity entity, GuestReplicator guest, ReplicaTrack track, double3 previousOrigin, double3 origin, uint sequence)
        {
            var predictor = guest.Predictor;
            var pose = em.GetComponentData<LocalTransform>(entity);
            var velocity = em.GetComponentData<PhysicsVelocity>(entity);
            var mass = em.GetComponentData<PhysicsMass>(entity);
            quaternion inertia = math.mul(pose.Rotation, mass.InertiaOrientation);
            var current = new OwnShipPredictor.BodyState
            {
                Sequence = predictor.AppliedSequence, Position = (double3)pose.Position + previousOrigin, Rotation = pose.Rotation,
                Linear = velocity.Linear, Angular = math.rotate(inertia, velocity.Angular)
            };
            var latest = track.Latest;
            var host = new OwnShipPredictor.BodyState { Position = latest.Position, Rotation = math.normalizesafe(latest.Rotation, quaternion.identity), Linear = latest.Linear, Angular = latest.Angular };
            bool corrected;
            if (!predictor.HasApplied)
            {
                // Just spawned: wait for an acknowledged snapshot, start exactly where the host has the ship, then predict from there.
                if (latest.HasAck == 0) return;
                current = host; corrected = true;
            }
            else
            {
                predictor.Record(current);
                corrected = latest.HasAck != 0 && predictor.Reconcile(latest.Ack, host, ref current);
            }
            if (corrected || math.any(previousOrigin != origin))
            {
                pose.Position = (float3)(current.Position - origin); pose.Rotation = current.Rotation;
                em.SetComponentData(entity, pose);
                velocity.Linear = current.Linear;
                velocity.Angular = math.rotate(math.inverse(math.mul(current.Rotation, mass.InertiaOrientation)), current.Angular);
                em.SetComponentData(entity, velocity);
            }
            predictor.AppliedSequence = sequence; predictor.HasApplied = true;
        }
    }

    /// <summary>Host: once per frame after the fixed step (WorldGroup, last), streams grid and chunk changes, transforms every second
    /// tick (30 Hz) and cell hashes every five seconds or on request (SPEC-90 HostCaptureSystem, HostSnapshotSystem,
    /// CellHashCheckSystem). Outside the fixed step, so the GameplayEnd command buffer stays the tick's last structural point.</summary>
    [UpdateInGroup(typeof(WorldGroup), OrderLast = true)]
    public partial class HostCaptureSystem : NetSystemBase
    {
        uint lastHashTick, lastSnapshotTick;
        protected override void OnCreate() { base.OnCreate(); RequireForUpdate<GameplayActive>(); RequireForUpdate<SimClock>(); }
        protected override void OnUpdate()
        {
            var host = Net?.Host;
            if (host == null) return;
            var em = EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            double3 origin = SystemAPI.TryGetSingleton<FrameOrigin>(out var frame) ? frame.OriginInStarSystem : double3.zero;
            Net.HostOriginForCommands = origin;
            host.Capture(em, tick);
            if (tick - lastSnapshotTick >= NetProtocol.SnapshotEveryTicks) { host.Snapshot(em, tick, origin, 3); lastSnapshotTick = tick; }
            if (Net.HashRequested || tick - lastHashTick >= 300)
            {
                host.SendHashReport(em, tick, Net.HashRequested);
                Net.HashRequested = false; lastHashTick = tick;
            }
        }
    }

    /// <summary>Host: each fixed tick, after DamageResolveSystem and before the structural sync, records which bricks lost cells only as
    /// detached bodies this tick, so guests skip debris for cells that arrive as hulks and chunk bodies instead.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(Game.Voxel.Damage.DamageResolveSystem))]
    [UpdateBefore(typeof(GameplayEndCommandBufferSystem))]
    public partial class HostRemovalTapSystem : NetSystemBase
    {
        protected override void OnCreate() { base.OnCreate(); RequireForUpdate<GameplayActive>(); }
        protected override void OnUpdate() { Net?.Host?.TapRemovals(EntityManager); }
    }

    /// <summary>Guest: sends the commands of the fixed ticks that ran, with a few older ones against loss (SPEC-90 GuestCommandSendSystem).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup), OrderLast = true)]
    public partial class GuestCommandSendSystem : NetSystemBase
    {
        protected override void OnUpdate() { if (Net?.Guest != null) Net.SendCommands(); }
    }

    /// <summary>Host and guest: moves this frame's queued messages into the driver, last in presentation (SPEC-90 NetSendSystem).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup), OrderLast = true), UpdateAfter(typeof(GuestCommandSendSystem))]
    public partial class NetSendSystem : NetSystemBase
    {
        protected override void OnUpdate() { Net?.Transport.Flush(); }
    }
}
