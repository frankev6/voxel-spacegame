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

    /// <summary>Host: copies each guest's newest command onto its ship (SPEC-90 NET-006). The fixed step turns it into ShipCommand.</summary>
    [UpdateInGroup(typeof(GameInitGroup)), UpdateBefore(typeof(GridSpawnSystem))]
    public partial class RemotePilotReceiveSystem : NetSystemBase
    {
        protected override void OnUpdate()
        {
            if (Net?.Host == null) return;
            var em = EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<RemotePilot>(), ComponentType.ReadWrite<RemotePilotInput>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            em.CompleteDependencyBeforeRW<RemotePilotInput>();
            foreach (var entity in entities)
            {
                byte slot = em.GetComponentData<RemotePilot>(entity).Slot;
                if (!Net.TryGetCommand(slot, out uint sequence, out var command)) continue;
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
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup), OrderFirst = true)]
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
            if (guest == null || !guest.HasOrigin) return;
            var em = EntityManager;
            em.CompleteAllTrackedJobs();
            double3 origin = guest.HostOrigin;
            if (SystemAPI.TryGetSingletonRW<FrameOrigin>(out var frame) && math.any(frame.ValueRO.OriginInStarSystem != origin))
                frame.ValueRW.OriginInStarSystem = origin;
            double tick = guest.RenderTick;
            using var entities = replicas.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var replica = em.GetComponentData<Replicated>(entity);
                if (!guest.TryGetTrack(replica.NetId, out var track) || !track.Sample(tick, NetProtocol.MaxExtrapolationTicks, out var s)) continue;
                var pose = em.GetComponentData<LocalTransform>(entity);
                pose.Position = (float3)(s.Position - origin); pose.Rotation = math.normalizesafe(s.Rotation, quaternion.identity);
                if (NetIds.IsChunk(replica.NetId)) pose.Scale = s.Scale;
                em.SetComponentData(entity, pose);
                if (em.HasComponent<PhysicsVelocity>(entity)) em.SetComponentData(entity, new PhysicsVelocity { Linear = s.Linear });
                if (s.HasCommand != 0 && replica.OwnedLocally == 0 && em.HasComponent<ShipCommand>(entity))
                {
                    var command = s.Command; command.AimWorld -= (float3)origin; command.Edges = ShipCommandEdges.None;
                    em.SetComponentData(entity, command);
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
    }

    /// <summary>Host: after the tick's single structural sync, streams grid and chunk changes (every tick), transforms (30 Hz) and
    /// cell hashes (every five seconds, or on request) to guests (SPEC-90 HostCaptureSystem, HostSnapshotSystem, CellHashCheckSystem).</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderLast = true), UpdateAfter(typeof(GameplayEndCommandBufferSystem))]
    public partial class HostCaptureSystem : NetSystemBase
    {
        uint lastHashTick;
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
            if (tick % NetProtocol.SnapshotEveryTicks == 0) host.Snapshot(em, tick, origin, 3);
            if (Net.HashRequested || tick - lastHashTick >= 300)
            {
                host.SendHashReport(em, tick, Net.HashRequested);
                Net.HashRequested = false; lastHashTick = tick;
            }
        }
    }

    /// <summary>Guest: sends the local PlayerCommand sampled this frame, at most 60 times a second (SPEC-90 GuestCommandSendSystem).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup), OrderLast = true)]
    public partial class GuestCommandSendSystem : NetSystemBase
    {
        double next;
        protected override void OnUpdate()
        {
            if (Net?.Guest == null || !SystemAPI.TryGetSingleton<PlayerCommand>(out var command)) return;
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (now < next) return;
            next = now + 1.0 / 60;
            Net.SendCommand(command, Net.Guest.HostOrigin);
        }
    }

    /// <summary>Host and guest: moves this frame's queued messages into the driver, last in presentation (SPEC-90 NetSendSystem).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup), OrderLast = true), UpdateAfter(typeof(GuestCommandSendSystem))]
    public partial class NetSendSystem : NetSystemBase
    {
        protected override void OnUpdate() { Net?.Transport.Flush(); }
    }
}
