using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Travel;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-32 §4.5 full travel surface; owning TRV tasks supply behavior, allocation and lifecycle.</summary>
    public sealed class TravelService : IGalaxyFrameTick, ILaneAccess, ISaveBlocker, ISceneExiting, IDisposable
    {
        public TravelService(GalaxyService galaxy, ITravelLoader loader, DockingService docking, World world,
            ContentCatalog catalog, IPauseService pause, IAudioService audio) => throw new NotImplementedException("TRV-006");
        public int TickOrder => 200;
        public int SceneExitOrder => 90;
        public TravelPhase Phase => throw new NotImplementedException("TRV-006");
        public TravelKind Kind => throw new NotImplementedException("TRV-006");
        public StarSystemId Target => throw new NotImplementedException("TRV-006");
        public bool CoverRequested => throw new NotImplementedException("TRV-006");
        public ArrivalPlan LastArrival => throw new NotImplementedException("TRV-006");
        public TravelStats Stats => throw new NotImplementedException("TRV-006");
        public TravelConfigDefinition Config => throw new NotImplementedException("TRV-006");

        public JumpRequestResult CanJump(StarSystemId target, JumpSource source) => throw new NotImplementedException("TRV-006");
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

        public bool IsLaneOpen(int laneIndex) => throw new NotImplementedException("TRV-003");
        public string GetLockedMessage(int laneIndex) => throw new NotImplementedException("TRV-003");
        public int GetFuelCost(int laneIndex) => throw new NotImplementedException("TRV-003");
        public uint Revision => throw new NotImplementedException("TRV-003");

        public JumpRequestResult CanCruise(double3 targetPositionM, float targetRadiusM) => throw new NotImplementedException("TRV-013");
        public JumpRequestResult RequestCruise(double3 targetPositionM, float targetRadiusM) => throw new NotImplementedException("TRV-013");
        public void Tick(in GalaxyFrameContext ctx) => throw new NotImplementedException("TRV-006");
        public SaveBlockReason GetSaveBlock() => throw new NotImplementedException("TRV-006");
        public void OnSceneExiting(in SceneExitContext context) => throw new NotImplementedException("TRV-006");
        public void Dispose() => throw new NotImplementedException("TRV-006");
    }
}
