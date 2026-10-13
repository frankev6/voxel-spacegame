using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using Unity.Transforms;
namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 state-driven plumes presentation; bounded session-owned buffers.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(FxUploadSystem))]
    public partial class ThrusterPlumeBridgeSystem : SystemBase, IFxRigConsumer
    {
        static readonly ProfilerMarker Gather = new ProfilerMarker(ProfilerCategory.Scripts, "ThrusterPlumeBridgeSystem.Gather");
        static readonly ProfilerMarker Draw = new ProfilerMarker(ProfilerCategory.Scripts, "ThrusterPlumeBridgeSystem.Draw");
        public FxRig Rig { get; set; }
        NativeReference<int> count;
        ComponentLookup<LocalToWorld> transforms; ComponentLookup<Hulk> hulks;
        protected override void OnCreate() { RequireForUpdate<GameplayActive>(); count = new NativeReference<int>(Allocator.Persistent); transforms = GetComponentLookup<LocalToWorld>(true); hulks = GetComponentLookup<Hulk>(true); }
        protected override void OnUpdate()
        {
            var batch = Rig?.Plumes; if (batch == null) return; batch.Begin();
            // Presentation reads after fixed-step producers; complete their component writers before the synchronous gather.
            EntityManager.CompleteDependencyBeforeRO<ThrusterState>(); EntityManager.CompleteDependencyBeforeRO<ThrusterVisual>(); EntityManager.CompleteDependencyBeforeRO<FunctionalBlock>(); EntityManager.CompleteDependencyBeforeRO<Hulk>(); EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            count.Value = 0; transforms.Update(this); hulks.Update(this);
            using (Gather.Auto()) new PlumeGatherJob { Output = batch.Data, Count = count, Cap = Rig.Budget.MaxPlumes, Transforms = transforms, Hulks = hulks, Minimum = Rig.Config.PlumeMinIntensity, Length = Rig.Config.PlumeLengthAtFullIntensity, Color = Rig.DefaultColor(FxKind.Thruster) }.Run();
            batch.Count = count.Value; using (Draw.Auto()) batch.Draw(); Rig.Stats.PlumesDrawn = count.Value;
        }
        protected override void OnDestroy() { Dependency.Complete(); count.Dispose(); }
    }
}
