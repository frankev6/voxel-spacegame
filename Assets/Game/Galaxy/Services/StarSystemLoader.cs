using System;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4. SYS-001 contract shell; SYS-004/006/007/013 own streaming behaviour.</summary>
    // SPEC-31 Q6: ITravelLoader is attached by TRV-006; do not create a second copy of SPEC-32's future interface.
    public sealed class StarSystemLoader : IGalaxyFrameTick, IGalaxyLiveDeltaSink, ISceneReady, ISceneExiting, IDisposable
    {
        public StarSystemLoader(GalaxyService galaxy, Unity.Entities.World world, Game.Voxel.Grid.VoxelStructureRegistry registry,
            Game.Voxel.Grid.BlueprintLibrary blueprints, IPersistentIdAllocator ids, Game.Data.Services.ContentCatalog catalog)
            => throw new NotImplementedException("SYS-004");
        public int TickOrder => 100;
        public int SceneReadyOrder => SceneHookOrder.Galaxy;
        public int SceneExitOrder => SceneHookOrder.Galaxy;
        public LoaderState State => throw new NotImplementedException("SYS-004");
        public StarSystemId Loaded => throw new NotImplementedException("SYS-004");
        public bool IsReady => State == LoaderState.Ready;
        public bool BeginStarSystemChange(StarSystemId target, double3 arrivalPositionM, float arrivalYawRad, StarSystemEntryReason reason)
            => throw new NotImplementedException("SYS-013");
        public bool IsArrivalReady => throw new NotImplementedException("TRV-006");
        public bool IsAreaReady(double3 positionM) => throw new NotImplementedException("TRV-006");
        public void Tick(in GalaxyFrameContext ctx) => throw new NotImplementedException("SYS-004");
        public void OnBodyDeltaChanged(BodyId body) => throw new NotImplementedException("SYS-004");
        public void FlushStructures() => throw new NotImplementedException("SYS-006");
        public void OnSceneReady(in SceneReadyContext context) => throw new NotImplementedException("SYS-004");
        public bool IsSceneReadyComplete(in SceneReadyContext context) => throw new NotImplementedException("SYS-004");
        public void OnSceneExiting(in SceneExitContext context) => throw new NotImplementedException("SYS-004");
        public void Dispose() => throw new NotImplementedException("SYS-004");
    }
}
