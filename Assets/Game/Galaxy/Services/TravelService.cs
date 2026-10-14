using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Core.Components;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Travel;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Unity.Collections;
using Unity.Transforms;
using Unity.Physics;
using Game.Voxel.PresentationStreams;
using Unity.Entities;
using Unity.Mathematics;
using RouteState = Game.Galaxy.Travel.RouteStatus;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-32 §4.5 full travel surface; owning TRV tasks supply behavior, allocation and lifecycle.</summary>
    public sealed class TravelService : IGalaxyFrameTick, ILaneAccess, ISaveBlocker, ISceneExiting, IDisposable
    {
        readonly GalaxyService galaxy;
        readonly ITravelLoader loader;
        readonly DockingService docking;
        readonly World world;
        readonly ContentCatalog catalog;
        readonly IPauseService pause;
        readonly IAudioService audio;
        readonly LaneAccessTable lanes = new();
        readonly RouteScratch routeScratch = new();
        readonly List<int> routeLanes = new();
        readonly List<StarSystemId> routeSystems = new();
        readonly IReadOnlyList<StarSystemId> routeView;
        RouteState routeStatus;
        string routeBlockedReason = "";
        int routeFuelCost, routeGeneration = -1, flagsGeneration = -1;
        uint routeStateRevision, routeLaneRevision, flagsRevision;
        StarSystemId routeDestination, routeSource;
        bool routeStamped, hasNextHop;
        NextHop nextHop;
        readonly EntityQuery worldScene, players, clocks, controlLocks;
        PauseToken pauseToken;
        IJumpEscorts escorts;
        StarSystemId jumpFrom;
        JumpSource jumpSource;
        int jumpLane, readyFrames;
        uint spoolStartTick, arrivalStartTick;
        float transitSeconds;
        bool timeoutReported;
        readonly int fuelIndex;
        uint lastRefreshTick;
        bool disposed;
        public TravelService(GalaxyService galaxy, ITravelLoader loader, DockingService docking, World world,
            ContentCatalog catalog, IPauseService pause, IAudioService audio)
        {
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (!world.IsCreated) throw new ArgumentException("World must be alive.", nameof(world));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.pause = pause ?? throw new ArgumentNullException(nameof(pause));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            routeView = routeSystems.AsReadOnly();
            Config = catalog.Get<TravelConfigDefinition>(new ContentId("config_travel"));
            fuelIndex = catalog.IndexOf(Config.FuelItem);
            if (fuelIndex == 0) GameLog.WarnOnce("TRV.Fuel.NoItem", LogCategories.Travel, "Travel fuel is not in the catalog; fuel costs are not enforced.");
            worldScene = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldSceneActive>());
            players = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>());
            clocks = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            controlLocks = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerControlLock>());
            EventBus<BodyInteracted>.Subscribe(OnBodyInteracted);
            EventBus<StarSystemEntered>.Subscribe(OnStarSystemEntered);
        }
        public int TickOrder => 200;
        public int SceneExitOrder => 90;
        public TravelPhase Phase { get; private set; }
        public TravelKind Kind { get; private set; }
        public StarSystemId Target { get; private set; }
        public bool CoverRequested { get; private set; }
        public ArrivalPlan LastArrival { get; private set; }
        public TravelStats Stats { get; private set; }
        public TravelConfigDefinition Config { get; }

        public JumpRequestResult CanJump(StarSystemId target, JumpSource source)
        {
            var g = galaxy.Galaxy;
            if (g == null) return JumpRequestResult.Refuse(JumpRefusal.NoGalaxy, "No galaxy is loaded.");
            if (disposed || !world.IsCreated || worldScene.IsEmptyIgnoreFilter)
                return JumpRequestResult.Refuse(JumpRefusal.NotInWorld, "You can only travel in space.");
            if (Phase != TravelPhase.Idle || !loader.IsReady)
                return JumpRequestResult.Refuse(JumpRefusal.Busy, "Already travelling.");
            if (!g.TryGetIndex(target, out _)) return JumpRequestResult.Refuse(JumpRefusal.UnknownTarget, "Unknown destination.");
            if (target.Equals(loader.Loaded)) return JumpRequestResult.Refuse(JumpRefusal.SameStarSystem, "You are already there.");
            Entity player = FindPlayer();
            if (player == Entity.Null || world.EntityManager.HasComponent<Hulk>(player))
                return JumpRequestResult.Refuse(JumpRefusal.NoPlayerShip, "Your ship cannot jump.");
            if (docking.IsDocked && source != JumpSource.Narrative)
                return JumpRequestResult.Refuse(JumpRefusal.Docked, "Undock first.");
            bool hasLane = g.TryFindLane(loader.Loaded, target, out int lane);
            if (source == JumpSource.Narrative) return JumpRequestResult.Ok();
            if (!hasLane) return JumpRequestResult.Refuse(JumpRefusal.NoLane, "No lane leads there from here.");
            Game.Core.Services.TryGet<ILogicContext>(out var context);
            int gate = g.Lanes[lane].GateIndex;
            if (gate != 0)
            {
                var spec = g.LaneGates[gate - 1];
                var condition = spec.Condition;
                bool open = condition == null || condition.IsAlwaysTrue ||
                    !condition.HasErrors && context != null && condition.Evaluate(context);
                if (!open) return JumpRequestResult.Refuse(JumpRefusal.LaneLocked,
                    string.IsNullOrEmpty(spec.LockedMessage) ? "This lane is locked." : spec.LockedMessage);
            }
            if (source == JumpSource.Gate)
            {
                int cost = FuelMath.Cost(g.Lanes[lane], Config.FuelPerLy, Config.FuelMinPerJump, Config.AncientGateFuel);
                int held = fuelIndex == 0 ? -1 : context?.Economy?.GetItemCount(fuelIndex) ?? -1;
                if (held >= 0 && held < cost) return JumpRequestResult.Refuse(JumpRefusal.NotEnoughFuel,
                    $"Not enough fuel: {cost} needed, {held} in the hold.");
            }
            return JumpRequestResult.Ok();
        }
        Entity FindPlayer()
        {
            using var entities = players.ToEntityArray(Allocator.Temp);
            Entity selected = Entity.Null;
            foreach (var entity in entities)
                if (selected == Entity.Null || entity.Index < selected.Index) selected = entity;
            return selected;
        }
        public JumpRequestResult RequestJump(StarSystemId target, JumpSource source)
        {
            var result = CanJump(target, source);
            if (!result.Accepted) return result;
            if (source == JumpSource.Narrative && docking.IsDocked) docking.Undock();
            if (players.CalculateEntityCount() > 1)
                GameLog.WarnOnce("TRV.MultiplePlayers", LogCategories.Travel, "Multiple player ships; travel uses the lowest entity index.");
            Kind = TravelKind.LaneJump; Target = target; jumpFrom = loader.Loaded; jumpSource = source;
            galaxy.Galaxy.TryFindLane(jumpFrom, target, out jumpLane);
            spoolStartTick = Clock.Tick;
            galaxy.PrefetchContent(target);
            WriteLock(PlayerControlLockFlags.Movement | PlayerControlLockFlags.Actions);
            var direction = galaxy.Galaxy.Get(target).PositionLy - galaxy.Galaxy.Get(jumpFrom).PositionLy;
            EmitWarp(math.normalizesafe(new float3((float)direction.x, 0, (float)direction.y), new float3(0, 0, 1)));
            if (Config.DepartCue != null) audio.PlayCue(Config.DepartCue.Id);
            Phase = TravelPhase.Spooling; CoverRequested = true;
            return result;
        }
        SimClock Clock => clocks.IsEmptyIgnoreFilter ? default : clocks.GetSingleton<SimClock>();
        void WriteLock(PlayerControlLockFlags flags)
        {
            if (!world.IsCreated) return;
            if (controlLocks.IsEmptyIgnoreFilter)
            { GameLog.ErrorOnce("TRV.Lock.Missing", LogCategories.Travel, "Player control lock is missing."); return; }
            // Read current singleton state so recreated entities and scene resets self-heal.
            if (controlLocks.GetSingleton<PlayerControlLock>().Flags != flags)
                world.EntityManager.SetComponentData(controlLocks.GetSingletonEntity(), new PlayerControlLock { Flags = flags });
        }
        void EmitWarp(float3 direction)
        {
            var player = FindPlayer(); var em = world.EntityManager;
            if (player == Entity.Null || !em.HasComponent<LocalTransform>(player)) return;
            var transform = em.GetComponentData<LocalTransform>(player);
            var velocity = em.HasComponent<PhysicsVelocity>(player) ? em.GetComponentData<PhysicsVelocity>(player).Linear : float3.zero;
            FxEvents.EnqueueMainThread(em, FxEvents.Warp(transform.Position, direction, velocity, Config.WarpFxScale));
        }
        void OnBodyInteracted(BodyInteracted interaction)
        {
            if (disposed || interaction.Kind != (byte)InteractionKind.LaneGate ||
                interaction.StarSystemValue != loader.Loaded.Value || galaxy.Galaxy == null || !loader.Loaded.IsValid) return;
            if (!TryResolveGate(interaction.BodyKey, out _, out var target))
            {
                GameLog.WarnOnce("TRV.Gate.NoLane", LogCategories.Travel, "The interacted gate has no lane.");
                return;
            }
            var result = RequestJump(target, JumpSource.Gate);
            if (!result.Accepted && result.Refusal != JumpRefusal.Busy)
                EventBus<TravelNotice>.Publish(new TravelNotice(TravelNoticeKind.JumpRefused, target.Value, result.Reason));
        }
        void CommitJump()
        {
            var content = galaxy.GetContent(Target);
            var plan = ArrivalSolver.SolveJump(content, jumpLane, galaxy.Galaxy.Get(Target).PositionLy,
                galaxy.Galaxy.Get(jumpFrom).PositionLy, Clock.UniversalTime, new ArrivalParams {
                    StandoffM = Config.ArrivalStandoffM, StepM = Config.ArrivalStepM,
                    ClearanceM = Config.PlayerClearanceM, MaxSteps = Config.ArrivalMaxSteps });
            int cost = GetFuelCost(jumpLane), held = FuelHeld;
            if (jumpSource == JumpSource.Gate && held >= 0 && held < cost)
            { Abort($"Not enough fuel: {cost} needed, {held} in the hold."); return; }
            Game.Core.Services.TryGet<IJumpEscorts>(out escorts);
            escorts?.CaptureEscorts(Config.EscortRadiusM);
            if (!loader.BeginStarSystemChange(Target, plan.PositionM, plan.YawRad, StarSystemEntryReason.Jump))
            { escorts?.RestoreEscorts(); escorts = null; Abort("Jump aborted: the star system is busy."); return; }
            if (jumpSource == JumpSource.Gate && held >= 0)
            {
                Game.Core.Services.TryGet<ILogicContext>(out var context);
                if (context?.Economy == null || !context.Economy.TryTakeItems(fuelIndex, cost))
                    GameLog.Error(LogCategories.Travel, "TRV.Fuel.Payment: fuel changed during jump commit.");
            }
            pauseToken = pause.Acquire("Travel"); LastArrival = plan; Phase = TravelPhase.Transit;
            transitSeconds = 0; readyFrames = 0; timeoutReported = false;
        }
        void BeginArrival()
        {
            escorts?.PlaceEscorts(LastArrival.PositionM, LastArrival.YawRad); escorts = null;
            pauseToken.Dispose(); pauseToken = default;
            Phase = TravelPhase.Arriving; arrivalStartTick = Clock.Tick; CoverRequested = false;
            var player = FindPlayer();
            var forward = player != Entity.Null && world.EntityManager.HasComponent<LocalTransform>(player)
                ? math.mul(world.EntityManager.GetComponentData<LocalTransform>(player).Rotation, new float3(0, 0, 1)) : new float3(0, 0, 1);
            EmitWarp(forward);
            if (Config.ArriveCue != null) audio.PlayCue(Config.ArriveCue.Id);
            var stats = Stats; stats.LastTransitSeconds = transitSeconds;
            stats.MaxTransitSeconds = math.max(stats.MaxTransitSeconds, transitSeconds); stats.JumpsCompleted++; Stats = stats;
            EventBus<LaneJumped>.Publish(new LaneJumped(jumpFrom.Value, Target.Value, jumpLane, jumpSource));
        }
        void Abort(string text, bool silent = false)
        {
            bool active = Phase != TravelPhase.Idle; var target = Target;
            pauseToken.Dispose(); pauseToken = default; escorts = null;
            WriteLock(PlayerControlLockFlags.None); CoverRequested = false;
            if (active && !target.Equals(loader.Loaded)) galaxy.ReleaseContent(target);
            Phase = TravelPhase.Idle; Kind = TravelKind.None; Target = default;
            if (!active) return;
            var stats = Stats; stats.JumpsAborted++; Stats = stats;
            if (!silent) EventBus<TravelNotice>.Publish(new TravelNotice(TravelNoticeKind.JumpAborted, target.Value, text));
        }
        bool TryResolveGate(ulong key, out int laneIndex, out StarSystemId target)
        {
            laneIndex = -1; target = default;
            var g = galaxy.Galaxy;
            if (disposed || g == null || !loader.Loaded.IsValid || !g.TryGetIndex(loader.Loaded, out _)) return false;
            var content = galaxy.GetContent(loader.Loaded);
            if (!content.TryFind(key, out int index)) return false;
            var body = content.Bodies[index];
            if (body.Id.Role != BodyRole.LaneGate || body.LaneIndex == GalaxyConstants.NoIndex ||
                body.LaneIndex >= g.Lanes.Length) return false;
            var lane = g.Lanes[body.LaneIndex];
            if (!lane.A.Equals(loader.Loaded) && !lane.B.Equals(loader.Loaded)) return false;
            laneIndex = body.LaneIndex; target = GalaxyDef.Neighbour(lane, loader.Loaded);
            return true;
        }
        public bool TryGetGateInfo(ulong gateKey, out GateInfo info)
        {
            info = default;
            if (!TryResolveGate(gateKey, out int lane, out var target)) return false;
            bool known = galaxy.State.IsKnown(target);
            int cost = GetFuelCost(lane), held = FuelHeld;
            info = new GateInfo {
                LaneIndex = lane, Target = target, Known = known,
                TargetName = known ? galaxy.GetStarSystemName(target) : "Uncharted",
                Open = IsLaneOpen(lane), LockedMessage = GetLockedMessage(lane),
                FuelCost = cost, FuelHeld = held, CanAfford = held < 0 || held >= cost,
                OnRoute = TryGetNextHop(out var hop) && hop.LaneIndex == lane
            };
            return true;
        }

        public StarSystemId Destination => galaxy.State.RouteDestination;
        public RouteStatus RouteStatus => routeStatus;
        public string RouteBlockedReason => routeBlockedReason;
        public IReadOnlyList<StarSystemId> Route => routeView;
        public int RouteFuelCost => routeFuelCost;
        public bool CanSetDestination(StarSystemId id, out string reason)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TravelService));
            EnsureLaneState(); var g = galaxy.Galaxy;
            if (g == null) { reason = "No galaxy is loaded."; return false; }
            if (!g.TryGetIndex(id, out int target) || !galaxy.State.IsKnown(id))
            { reason = "Uncharted star system."; return false; }
            var source = loader.Loaded;
            if (id.Equals(source)) { reason = "You are already there."; return false; }
            PrepareRouteInputs(g);
            if (g.TryGetIndex(source, out int start))
            {
                if (RoutePlanner.TryFindRoute(g, routeScratch, start, target, false, routeLanes, out _))
                { reason = ""; return true; }
                if (RoutePlanner.TryFindRoute(g, routeScratch, start, target, true, routeLanes, out _))
                { reason = FirstLockedRouteMessage(); return false; }
            }
            reason = "No known route."; return false;
        }
        public bool SetDestination(StarSystemId id, out string reason)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TravelService));
            if (id.Equals(Destination)) { reason = ""; return true; }
            if (!CanSetDestination(id, out reason)) return false;
            galaxy.State.RouteDestination = id; RecomputeRoute(); return true;
        }
        public void ClearDestination()
        {
            if (disposed) throw new ObjectDisposedException(nameof(TravelService));
            galaxy.State.RouteDestination = default; ResetRouteCache(); routeStamped = false;
        }
        public bool TryGetNextHop(out NextHop hop)
        { hop = routeStatus == RouteState.Ok && hasNextHop ? nextHop : default; return routeStatus == RouteState.Ok && hasNextHop; }
        void PrepareRouteInputs(GalaxyDef g)
        {
            routeScratch.EnsureCapacity(g.StarSystems.Length, g.LaneGates.Count, g.Lanes.Length);
            if (flagsGeneration != galaxy.GenerationCount || flagsRevision != galaxy.State.Revision)
            {
                for (int i = 0; i < routeScratch.SystemFlags.Length; i++) routeScratch.SystemFlags[i] = 0;
                foreach (uint id in galaxy.State.Known)
                    if (g.TryGetIndex(new StarSystemId(id), out int index)) routeScratch.SystemFlags[index] |= 1;
                foreach (uint id in galaxy.State.Visited)
                    if (g.TryGetIndex(new StarSystemId(id), out int index)) routeScratch.SystemFlags[index] |= 2;
                flagsGeneration = galaxy.GenerationCount; flagsRevision = galaxy.State.Revision;
            }
            for (int i = 0; i < routeScratch.GateOpen.Length; i++) routeScratch.GateOpen[i] = 0;
            for (int i = 0; i < lanes.GateOpen.Length; i++) routeScratch.GateOpen[i] = lanes.GateOpen[i] ? (byte)1 : (byte)0;
        }
        string FirstLockedRouteMessage()
        {
            foreach (int lane in routeLanes) if (!IsLaneOpen(lane)) return GetLockedMessage(lane);
            return "This lane is locked.";
        }
        void ResetRouteCache()
        {
            routeSystems.Clear(); routeFuelCost = 0; routeBlockedReason = ""; routeStatus = RouteState.None;
            nextHop = default; hasNextHop = false;
        }
        void UpdateRoute()
        {
            // No destination requires no loader access, native search, statistics or notice.
            if (!Destination.IsValid || galaxy.Galaxy == null) { ResetRouteCache(); routeStamped = false; return; }
            if (!routeStamped || !routeDestination.Equals(Destination) || !routeSource.Equals(loader.Loaded) ||
                routeLaneRevision != lanes.Revision || routeStateRevision != galaxy.State.Revision || routeGeneration != galaxy.GenerationCount)
                RecomputeRoute();
        }
        void RecomputeRoute()
        {
            var g = galaxy.Galaxy; var destination = Destination; var source = loader.Loaded;
            var previous = routeStatus; ResetRouteCache();
            if (!destination.IsValid || g == null) { routeStamped = false; return; }
            if (!g.TryGetIndex(destination, out int target)) { ClearDestination(); return; }
            if (destination.Equals(source))
            {
                EventBus<TravelNotice>.Publish(new TravelNotice(TravelNoticeKind.DestinationReached, destination.Value,
                    "Arrived at " + galaxy.GetStarSystemName(destination) + "."));
                ClearDestination(); return;
            }
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            PrepareRouteInputs(g);
            bool found = g.TryGetIndex(source, out int start) &&
                RoutePlanner.TryFindRoute(g, routeScratch, start, target, false, routeLanes, out _);
            if (found)
            {
                routeStatus = RouteState.Ok; var at = source; long fuel = 0;
                foreach (int lane in routeLanes)
                { at = GalaxyDef.Neighbour(g.Lanes[lane], at); routeSystems.Add(at); fuel += GetFuelCost(lane); }
                routeFuelCost = (int)System.Math.Min(fuel, int.MaxValue);
                if (routeLanes.Count != 0)
                {
                    int first = routeLanes[0]; var content = galaxy.GetContent(source);
                    foreach (var body in content.Bodies)
                        if (body.Id.Role == BodyRole.LaneGate && body.LaneIndex == first)
                        {
                            nextHop = new NextHop { Next = routeSystems[0], LaneIndex = first, GateKey = body.Id.LocalKey,
                                GatePositionM = OrbitMath.Position(body, Clock.UniversalTime) };
                            hasNextHop = true; break;
                        }
                }
            }
            else if (g.TryGetIndex(source, out start) && RoutePlanner.TryFindRoute(g, routeScratch, start, target, true, routeLanes, out _))
            { routeStatus = RouteState.Blocked; routeBlockedReason = FirstLockedRouteMessage(); }
            else { routeStatus = RouteState.Unreachable; routeBlockedReason = "No known route."; }
            if (previous == RouteState.Ok && (routeStatus == RouteState.Blocked || routeStatus == RouteState.Unreachable))
                EventBus<TravelNotice>.Publish(new TravelNotice(TravelNoticeKind.RouteBlocked, destination.Value, routeBlockedReason));
            else if ((previous == RouteState.Blocked || previous == RouteState.Unreachable) && routeStatus == RouteState.Ok)
                EventBus<TravelNotice>.Publish(new TravelNotice(TravelNoticeKind.RouteRestored, destination.Value,
                    "Route to " + galaxy.GetStarSystemName(destination) + " is open."));
            routeDestination = Destination; routeSource = source; routeLaneRevision = lanes.Revision;
            routeStateRevision = galaxy.State.Revision; routeGeneration = galaxy.GenerationCount; routeStamped = true;
            var stats = Stats; stats.RouteComputations++;
            stats.LastRouteMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency); Stats = stats;
        }

        public bool IsLaneOpen(int laneIndex)
        {
            EnsureLaneState(); var g = galaxy.Galaxy;
            if (disposed || g == null || (uint)laneIndex >= (uint)g.Lanes.Length) return false;
            int gate = g.Lanes[laneIndex].GateIndex;
            return gate == 0 || gate <= lanes.GateOpen.Length && lanes.GateOpen[gate - 1];
        }
        public string GetLockedMessage(int laneIndex)
        {
            EnsureLaneState(); var g = galaxy.Galaxy;
            if (disposed || g == null || (uint)laneIndex >= (uint)g.Lanes.Length || IsLaneOpen(laneIndex)) return "";
            int gate = g.Lanes[laneIndex].GateIndex;
            string message = gate > 0 && gate <= g.LaneGates.Count ? g.LaneGates[gate - 1].LockedMessage : "";
            return string.IsNullOrEmpty(message) ? "This lane is locked." : message;
        }
        public int GetFuelCost(int laneIndex)
        {
            EnsureLaneState(); var g = galaxy.Galaxy;
            return disposed || g == null || (uint)laneIndex >= (uint)g.Lanes.Length ? 0 : FuelMath.Cost(g.Lanes[laneIndex], Config.FuelPerLy, Config.FuelMinPerJump, Config.AncientGateFuel);
        }
        public uint Revision { get { EnsureLaneState(); return lanes.Revision; } }
        internal int FuelHeld
        {
            get
            {
                if (fuelIndex == 0) return -1;
                Game.Core.Services.TryGet<ILogicContext>(out var context);
                int held = context?.Economy?.GetItemCount(fuelIndex) ?? -1;
                if (held < 0) GameLog.WarnOnce("TRV.Fuel.NoEconomy", LogCategories.Travel, "Travel economy is unavailable; fuel costs are not enforced.");
                return held;
            }
        }
        void EnsureLaneState()
        {
            if (disposed) return;
            var g = galaxy.Galaxy;
            if (g == null) { if (lanes.BuiltForGeneration != -1) lanes.Dispose(); return; }
            Game.Core.Services.TryGet<ILogicContext>(out var context);
            if (lanes.BuiltForGeneration == galaxy.GenerationCount && !(lanes.PendingContext && context != null)) return;
            lanes.Dispose(); int count = g.LaneGates.Count;
            lanes.GateOpen = new bool[count]; lanes.Watchers = new LogicWatcher[count];
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var condition = g.LaneGates[i].Condition;
                    if (condition == null || condition.IsAlwaysTrue) lanes.GateOpen[i] = true;
                    else if (condition.HasErrors) GameLog.ErrorOnce("TRV.Lane.ConditionError." + i, LogCategories.Travel, "Lane condition failed compilation; the lane remains closed.");
                    else if (context == null)
                    { lanes.PendingContext = true; GameLog.WarnOnce("TRV.Lane.NoLogicContext", LogCategories.Travel, "Narrative context is unavailable; gated lanes remain closed."); }
                    else
                    {
                        int index = i;
                        var watcher = new LogicWatcher(condition, context, open => OnGateChanged(index, open));
                        lanes.Watchers[i] = watcher; lanes.GateOpen[i] = watcher.Value;
                    }
                }
                lanes.BuiltForGeneration = galaxy.GenerationCount; lanes.Revision++; lastRefreshTick = 0;
            }
            catch { lanes.Dispose(); throw; }
        }
        void OnGateChanged(int index, bool open)
        {
            if (disposed || (uint)index >= (uint)lanes.GateOpen.Length || lanes.GateOpen[index] == open) return;
            lanes.GateOpen[index] = open; lanes.Revision++;
        }
        void RefreshLaneConditions() { foreach (var watcher in lanes.Watchers) watcher?.Refresh(); }
        void OnStarSystemEntered(StarSystemEntered _) { EnsureLaneState(); if (!disposed) RefreshLaneConditions(); }

        public JumpRequestResult CanCruise(double3 targetPositionM, float targetRadiusM) => throw new NotImplementedException("TRV-013");
        public JumpRequestResult RequestCruise(double3 targetPositionM, float targetRadiusM) => throw new NotImplementedException("TRV-013");
        public void Tick(in GalaxyFrameContext ctx)
        {
            EnsureLaneState(); if (disposed) return;
            if (galaxy.Galaxy == null) { UpdateRoute(); return; }
            if (!world.IsCreated || worldScene.IsEmptyIgnoreFilter) return;
            if (unchecked(ctx.Tick - lastRefreshTick) >= Config.LaneRefreshTicks)
            { RefreshLaneConditions(); lastRefreshTick = ctx.Tick; }
            UpdateRoute();
            switch (Phase)
            {
                case TravelPhase.Idle: WriteLock(PlayerControlLockFlags.None); break;
                case TravelPhase.Spooling:
                    var player = FindPlayer();
                    if (player == Entity.Null || world.EntityManager.HasComponent<Hulk>(player))
                        Abort("Jump aborted: your ship cannot jump.");
                    else if (unchecked(ctx.Tick - spoolStartTick) >= Config.SpoolTicks) CommitJump();
                    break;
                case TravelPhase.Transit:
                    transitSeconds += ctx.UnscaledDeltaTime;
                    readyFrames = loader.IsArrivalReady ? readyFrames + 1 : 0;
                    bool loaded = loader.IsReady && loader.Loaded.Equals(Target);
                    if (loaded && loader.IsArrivalReady && readyFrames >= Config.RevealDelayFrames && transitSeconds >= Config.CoverMinSeconds)
                        BeginArrival();
                    else if (transitSeconds >= Config.TransitTimeoutSeconds)
                    {
                        if (!timeoutReported)
                        {
                            timeoutReported = true; var stats = Stats; stats.TransitTimeouts++; Stats = stats;
                            GameLog.Error(LogCategories.Travel, "TRV.TransitTimeout: loaded " + loader.Loaded +
                                ", ready " + loader.IsReady + ", arrival ready " + loader.IsArrivalReady);
                        }
                        if (loaded) BeginArrival();
                    }
                    break;
                case TravelPhase.Arriving:
                    if (unchecked(ctx.Tick - arrivalStartTick) >= Config.ArrivalTicks)
                    { WriteLock(PlayerControlLockFlags.None); Phase = TravelPhase.Idle; Kind = TravelKind.None; Target = default; }
                    break;
            }
        }
        public SaveBlockReason GetSaveBlock() => Phase == TravelPhase.Idle ? SaveBlockReason.None : SaveBlockReason.Transitioning;
        public void OnSceneExiting(in SceneExitContext context) => Abort("", true);
        public void Dispose()
        {
            if (disposed) return;
            Abort("", true); EventBus<BodyInteracted>.Unsubscribe(OnBodyInteracted);
            disposed = true; EventBus<StarSystemEntered>.Unsubscribe(OnStarSystemEntered); lanes.Dispose(); routeScratch.Dispose(); ResetRouteCache();
            if (world.IsCreated) { worldScene.Dispose(); players.Dispose(); clocks.Dispose(); controlLocks.Dispose(); }

        }
    }
}
