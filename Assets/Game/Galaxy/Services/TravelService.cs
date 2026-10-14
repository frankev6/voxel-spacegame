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
using Unity.Entities;
using Unity.Mathematics;

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
        readonly EntityQuery worldScene, players;
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
            Config = catalog.Get<TravelConfigDefinition>(new ContentId("config_travel"));
            fuelIndex = catalog.IndexOf(Config.FuelItem);
            if (fuelIndex == 0) GameLog.WarnOnce("TRV.Fuel.NoItem", LogCategories.Travel, "Travel fuel is not in the catalog; fuel costs are not enforced.");
            worldScene = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldSceneActive>());
            players = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>());
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
        public JumpRequestResult RequestJump(StarSystemId target, JumpSource source) => throw new NotImplementedException("TRV-006");
        public bool TryGetGateInfo(ulong gateKey, out GateInfo info) => throw new NotImplementedException("TRV-008");

        public StarSystemId Destination => throw new NotImplementedException("TRV-010");
        public RouteStatus RouteStatus => throw new NotImplementedException("TRV-010");
        public string RouteBlockedReason => throw new NotImplementedException("TRV-010");
        public IReadOnlyList<StarSystemId> Route => throw new NotImplementedException("TRV-010");
        public int RouteFuelCost => throw new NotImplementedException("TRV-010");
        public bool CanSetDestination(StarSystemId id, out string reason) => throw new NotImplementedException("TRV-010");
        public bool SetDestination(StarSystemId id, out string reason) => throw new NotImplementedException("TRV-010");
        public void ClearDestination() => throw new NotImplementedException("TRV-010");
        public bool TryGetNextHop(out NextHop hop) => throw new NotImplementedException("TRV-010");

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
            EnsureLaneState(); if (disposed || galaxy.Galaxy == null || !world.IsCreated || worldScene.IsEmptyIgnoreFilter) return;
            if (unchecked(ctx.Tick - lastRefreshTick) >= Config.LaneRefreshTicks)
            { RefreshLaneConditions(); lastRefreshTick = ctx.Tick; }
            // TODO(TRV-006): add jump execution after the lane refresh; no phase, lock or transit behavior is supplied here.
        }
        public SaveBlockReason GetSaveBlock() => throw new NotImplementedException("TRV-006");
        public void OnSceneExiting(in SceneExitContext context) => throw new NotImplementedException("TRV-006");
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; EventBus<StarSystemEntered>.Unsubscribe(OnStarSystemEntered); lanes.Dispose();
            if (world.IsCreated) { worldScene.Dispose(); players.Dispose(); }
            // TRV-006 owns any later execution resources, pause token and control lock.
        }
    }
}
