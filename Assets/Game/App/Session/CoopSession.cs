using System;
using System.Collections.Generic;
using Game.App.Flow;
using Game.App.Services;
using Game.App.Spawning;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Components;
using Game.Core.Net;
using Game.Data.Definitions;
using Game.Net;
using Game.Net.Systems;
using Game.Net.Transport;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.UI.Data;
using Game.Voxel.Grid;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.Session
{
    /// <summary>Co-op spike session (ADR-0116, SPEC-90 §5–6): hosting from the playground, joining into a replica World scene, the
    /// guests' ships on the host, role switching and the network pump. Main thread; ticked by GameRoot.</summary>
    public sealed partial class CoopSession : ICoopSession, ISceneReady, IDisposable
    {
        readonly World world;
        readonly SceneFlow flow;
        readonly GameSession session;
        readonly VoxelStructureRegistry registry;
        readonly PauseService pause;
        readonly GameConfigDefinition config;
        readonly Func<Game.UI.Services.UIRoot> ui;
        readonly Dictionary<byte, ulong> guestShips = new();
        readonly List<(ulong Id, double At)> drifting = new();
        readonly List<Game.Voxel.Model.VoxelStructure> guestStructures = new();   // flagship copies; freed with the session object
        CoopNet net;
        bool joinRequested, readySent;
        string status = "Offline";

        public CoopState State { get; private set; }
        public SimRoleKind Role => net?.Role ?? SimRoleKind.Solo;
        public string StatusText => status;
        public CoopNet Net => net;
        /// <summary>Blueprint for guests' ships; None = the configured start ship (SPEC-90 open question 4).</summary>
        public ContentId GuestBlueprint { get; set; }
        public string PlayerName { get; set; } = Environment.UserName;

        public CoopSession(World world, SceneFlow flow, GameSession session, VoxelStructureRegistry registry, PauseService pause,
            GameConfigDefinition config, Func<Game.UI.Services.UIRoot> ui)
        {
            this.world = world; this.flow = flow; this.session = session; this.registry = registry; this.pause = pause; this.config = config; this.ui = ui;
            flow.RegisterReady(this);
        }

        // ---------------- ICoopSession ----------------

        public bool Host(ushort port)
        {
            if (net != null) { status = "Already in a session; net leave first"; return false; }
            if (session.CurrentScene != GameScene.World || !session.IsSandbox || session.IsTransitioning)
            { status = "Host from the playground (World sandbox)"; return false; }
            net = new CoopNet(registry) { };
            net.Transport.LocalName = PlayerName;
            if (!net.StartHost(port)) { status = net.Transport.LastError; Shutdown(); return false; }
            BindDev();
            net.Transport.PeerReady += OnGuestReady;
            net.Transport.PeerLeft += OnGuestLeft;
            SetRole(SimRoleKind.Host);
            pause.KeepRunning = true;
            State = CoopState.Hosting; status = "Hosting on port " + port;
            return true;
        }

        public bool Join(string address, ushort port)
        {
            if (net != null) { status = "Already in a session; net leave first"; return false; }
            if (session.IsTransitioning) { status = "Busy"; return false; }
            net = new CoopNet(registry);
            net.Transport.LocalName = PlayerName;
            if (!net.StartGuest(address, port)) { status = net.Transport.LastError; Shutdown(); return false; }
            BindDev();
            net.Transport.Welcomed += OnWelcomed;
            net.Transport.Failed += OnFailed;
            State = CoopState.Connecting; status = "Connecting to " + address + ":" + port;
            return true;
        }

        public void Leave(string reason)
        {
            if (net == null) return;
            var role = net.Role;
            GameLog.Info(NetTransport.Category, "leaving the session: " + reason);
            if (role == SimRoleKind.Host) ReleaseGuestShips(true);
            net.Transport.Shutdown(reason);   // the goodbye carries the reason
            Shutdown();
            status = "Offline (" + reason + ")";
            if (role == SimRoleKind.Guest && session.CurrentScene == GameScene.World && !session.IsTransitioning)
                _ = session.QuitToMainMenuAsync(false);
        }

        public void GetPeers(List<CoopPeer> results)
        {
            results.Clear();
            if (net == null) return;
            foreach (var peer in net.Transport.Peers) results.Add(new CoopPeer(peer.Slot, peer.Name, peer.RttMs, peer.Phase == NetPeerPhase.InWorld));
        }

        // ---------------- lifecycle ----------------

        void SetRole(SimRoleKind role)
        {
            var em = world.EntityManager;
            using (var query = em.CreateEntityQuery(ComponentType.ReadWrite<SimRole>()))
                if (!query.IsEmptyIgnoreFilter) query.SetSingleton(new SimRole { Value = role });
            RoleSystemPolicy.Apply(world, role);
            foreach (var system in world.Systems)
                if (system is NetSystemBase netSystem) { if (role == SimRoleKind.Solo) netSystem.Unbind(); else netSystem.Bind(net); }
        }

        void Shutdown()
        {
            var old = net; net = null;
            joinRequested = false; readySent = false;
            pause.KeepRunning = false;
            HideOverlay();
            SetRole(SimRoleKind.Solo);
            old?.Dispose();
            State = CoopState.Offline;
        }

        /// <summary>GameRoot.Update: run the transport, then advance the join flow.</summary>
        public void Tick()
        {
            TickDev();
            if (net == null) return;
            net.Transport.Pump();
            if (net == null) return;   // a handler may have ended the session
            if (net.Role == SimRoleKind.Guest)
            {
                if (State == CoopState.Joining && readySent && net.Guest.JoinComplete) { State = CoopState.Joined; pause.KeepRunning = true; status = "Joined as slot " + net.Transport.LocalSlot; }
                else if (State == CoopState.Joined && session.CurrentScene != GameScene.World && !session.IsTransitioning) { Leave("left the World scene"); return; }
            }
            else UpdateDrifting();
            net.Transport.Flush();
            UpdateOverlay();
        }

        void Notify(string text, NotificationKind kind = NotificationKind.Info)
        {
            var root = ui?.Invoke();
            root?.Notifications.Post(new Notification { Kind = kind, Label = text });
        }

        // ---------------- guest ----------------

        void OnWelcomed(byte slot)
        {
            State = CoopState.Joining; status = "Joining as slot " + slot;
            if (joinRequested) return;
            joinRequested = true;
            _ = EnterReplicaWorld();
        }

        async UnityEngine.Awaitable EnterReplicaWorld()
        {
            try
            {
                if (session.HasActiveSession && session.CurrentScene != GameScene.MainMenu)
                {
                    var left = await session.QuitToMainMenuAsync(false);
                    if (left != FlowResult.Ok) { OnFailed("Could not leave the current scene: " + left); return; }
                }
                if (net == null) return;
                // Replica mode before the World scene runs: no AI, damage, splits or rebasing on this machine.
                SetRole(SimRoleKind.Guest);
                var entered = await flow.EnterSandboxAsync(new SandboxArgs(false, 42));
                if (entered != FlowResult.Ok) OnFailed("Could not enter the World scene: " + entered);
            }
            catch (Exception e) { GameLog.Exception(NetTransport.Category, e, "enter replica world"); OnFailed(e.Message); }
        }

        public int SceneReadyOrder => SceneHookOrder.Default;
        public void OnSceneReady(in SceneReadyContext context)
        {
            if (net == null || net.Role != SimRoleKind.Guest || context.Scene != GameScene.World || readySent) return;
            readySent = true;
            net.Guest.Reset(registry);
            net.Transport.SendReliable(net.Transport.HostPeer, Game.Net.Protocol.NetMessage.Ready, null);
            status = "Receiving the world";
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context) => true;

        void OnFailed(string reason)
        {
            GameLog.Warn(NetTransport.Category, reason);
            Notify(reason, NotificationKind.Warning);
            bool inReplica = net?.Role == SimRoleKind.Guest && session.CurrentScene == GameScene.World;
            Shutdown();
            State = CoopState.Failed; status = reason;
            if (inReplica && !session.IsTransitioning)
            {
                GameLog.Info(NetTransport.Category, "returning to the main menu");
                _ = session.QuitToMainMenuAsync(false);
            }
        }

        // ---------------- host ----------------

        void OnGuestReady(NetPeer peer)
        {
            try
            {
                if (!Game.Core.Services.TryGet<ShipSpawnService>(out var spawner)) throw new InvalidOperationException("no ship spawner");
                // SPEC-90 open question 4 default: guests fly a copy of the host's flagship (the playground ship, with its fitted shield).
                var blueprint = GuestBlueprint.IsNone ? config.StartBlueprint.Id : GuestBlueprint;
                Game.Voxel.Model.VoxelStructure copy = null;
                if (GuestBlueprint.IsNone && session.Fleet.Flagship?.Structure != null) { copy = session.Fleet.Flagship.Structure.Clone(); guestStructures.Add(copy); }
                float3 position = new float3(40 * peer.Slot, 0, -30);
                quaternion rotation = quaternion.identity;
                if (session.PlayerSpawner != null && session.PlayerSpawner.TryGetRoot(out var player))
                {
                    var pose = world.EntityManager.GetComponentData<LocalTransform>(player);
                    position = pose.Position + math.rotate(pose.Rotation, new float3(45 * (peer.Slot % 2 == 1 ? 1 : -1) * ((peer.Slot + 1) / 2), 0, -20));
                    rotation = pose.Rotation;
                }
                var id = spawner.Spawn(new ShipSpawnArgs { Blueprint = blueprint, Structure = copy, Position = position, Rotation = rotation, Allegiance = Allegiance.Player, WithAi = false });
                if (id.IsNone || !spawner.TryGetRoot(id, out var root)) throw new InvalidOperationException("the guest ship spawn was rejected");
                var em = world.EntityManager;
                em.AddComponentData(root, new RemotePilot { Slot = peer.Slot });
                em.AddComponentData(root, new Pilot { Slot = peer.Slot });   // ADR-0117: served by the same per-pilot systems as the host's ship
                em.AddComponentData(root, new RemotePilotInput());
                em.AddComponentData(root, new RemotePilotEdgeConsumed());
                em.AddComponentData(root, PilotName.From(string.IsNullOrEmpty(peer.Name) ? "Guest " + peer.Slot : peer.Name));
                guestShips[peer.Slot] = id.Value;
                Notify(peer.Name + " joined");
                status = "Hosting: " + net.Transport.Peers.Count + " guest(s)";
            }
            catch (Exception e)
            {
                GameLog.Exception(NetTransport.Category, e, "guest ship");
                net?.Transport.Disconnect(peer, "no ship for you: " + e.Message);
            }
        }

        void OnGuestLeft(NetPeer peer, string reason)
        {
            if (net == null || net.Role != SimRoleKind.Host) return;
            Notify((string.IsNullOrEmpty(peer.Name) ? "A guest" : peer.Name) + " left");
            if (!guestShips.Remove(peer.Slot, out var id)) return;
            // SPEC-90 §5: the ship drifts without a pilot and is removed after 30 s.
            if (Game.Core.Services.TryGet<ShipSpawnService>(out var spawner) && spawner.TryGetRoot(new PersistentId(id), out var root))
            {
                var em = world.EntityManager;
                if (em.HasComponent<RemotePilot>(root)) em.RemoveComponent<RemotePilot>(root);
                if (em.HasComponent<ShipCommand>(root)) em.SetComponentData(root, new ShipCommand { Flags = ShipCommandFlags.Brake });
            }
            drifting.Add((id, UnityEngine.Time.realtimeSinceStartupAsDouble + 30));
            status = "Hosting: " + net.Transport.Peers.Count + " guest(s)";
        }

        void UpdateDrifting()
        {
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            for (int i = drifting.Count - 1; i >= 0; i--)
            {
                if (drifting[i].At > now) continue;
                if (Game.Core.Services.TryGet<ShipSpawnService>(out var spawner)) spawner.Despawn(new PersistentId(drifting[i].Id));
                drifting.RemoveAt(i);
            }
        }

        void ReleaseGuestShips(bool despawn)
        {
            if (!despawn || !Game.Core.Services.TryGet<ShipSpawnService>(out var spawner)) { guestShips.Clear(); drifting.Clear(); return; }
            foreach (var id in guestShips.Values) spawner.Despawn(new PersistentId(id));
            foreach (var entry in drifting) spawner.Despawn(new PersistentId(entry.Id));
            guestShips.Clear(); drifting.Clear();
        }

        // SPEC-90 NET-010: one line of session health over the HUD while a session is live.
        UnityEngine.UIElements.Label overlay;
        double overlayAt;
        void UpdateOverlay()
        {
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (now < overlayAt) return;
            overlayAt = now + .5;
            var root = ui?.Invoke();
            if (root == null) return;
            if (overlay == null)
            {
                overlay = new UnityEngine.UIElements.Label { name = "coop-overlay", pickingMode = UnityEngine.UIElements.PickingMode.Ignore };
                overlay.AddToClassList("text-xs"); overlay.AddToClassList("text-muted");
                overlay.style.position = UnityEngine.UIElements.Position.Absolute; overlay.style.top = 8; overlay.style.left = new UnityEngine.UIElements.Length(50, UnityEngine.UIElements.LengthUnit.Percent);
                overlay.style.translate = new UnityEngine.UIElements.Translate(new UnityEngine.UIElements.Length(-50, UnityEngine.UIElements.LengthUnit.Percent), 0);
                root.GetLayer(UILayer.Hud).Add(overlay);
            }
            var s = net.Transport.Stats;
            var text = new System.Text.StringBuilder("CO-OP  ");
            if (net.Role == SimRoleKind.Host) text.Append("host · ").Append(net.Transport.Peers.Count).Append(" guest(s)");
            else text.Append("slot ").Append(net.Transport.LocalSlot).Append(" · ").Append(State == CoopState.Joined ? "joined" : State.ToString().ToLowerInvariant());
            float rtt = 0; foreach (var peer in net.Transport.Peers) rtt = UnityEngine.Mathf.Max(rtt, peer.RttMs);
            text.Append(" · ").Append(rtt.ToString("0")).Append(" ms · ↑").Append((s.OutKbps(NetChannel.Reliable) + s.OutKbps(NetChannel.Unreliable)).ToString("0"))
                .Append(" ↓").Append((s.InKbps(NetChannel.Reliable) + s.InKbps(NetChannel.Unreliable)).ToString("0")).Append(" kbps");
            if (s.Desyncs > 0) text.Append(" · desyncs ").Append(s.Desyncs);
            overlay.text = text.ToString();
        }

        void HideOverlay() { overlay?.RemoveFromHierarchy(); overlay = null; }

        partial void BindDev();
        partial void TickDev();

        /// <summary>Host, dev: puts a guest's ship beside the host's, facing the same way.</summary>
        public bool BringGuest(byte slot)
        {
            if (!guestShips.TryGetValue(slot, out var id) || !Game.Core.Services.TryGet<ShipSpawnService>(out var spawner)
                || !spawner.TryGetRoot(new PersistentId(id), out var root) || session.PlayerSpawner == null || !session.PlayerSpawner.TryGetRoot(out var player)) return false;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            var pose = em.GetComponentData<LocalTransform>(player);
            var target = em.GetComponentData<LocalTransform>(root);
            target.Position = pose.Position + math.rotate(pose.Rotation, new float3(35 * (slot % 2 == 1 ? 1 : -1), 0, -10)); target.Rotation = pose.Rotation;
            em.SetComponentData(root, target);
            if (em.HasComponent<Unity.Physics.PhysicsVelocity>(root)) em.SetComponentData(root, default(Unity.Physics.PhysicsVelocity));
            return true;
        }

        public void Dispose()
        {
            if (net != null) Shutdown();
            foreach (var s in guestStructures) if (!s.IsPinned) s.Dispose();
            guestStructures.Clear();
        }
    }
}
