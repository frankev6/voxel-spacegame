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
    /// <summary>SPEC-63 state-driven shields presentation; bounded session-owned buffers.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(FxUploadSystem))]
    public partial class ShieldRenderBridgeSystem : SystemBase, IFxRigConsumer
    {
        static readonly ProfilerMarker Gather = new ProfilerMarker(ProfilerCategory.Scripts, "ShieldRenderBridgeSystem.Gather");
        static readonly ProfilerMarker Draw = new ProfilerMarker(ProfilerCategory.Scripts, "ShieldRenderBridgeSystem.Draw");
        public FxRig Rig { get; set; }
        NativeReference<int> count;

        protected override void OnCreate() { RequireForUpdate<GameplayActive>(); count = new NativeReference<int>(Allocator.Persistent); RequireForUpdate<SimClock>(); }
        protected override void OnUpdate()
        {
            var batch = Rig?.Shields; if (batch == null) return; batch.Begin();
            // Presentation reads after fixed-step producers; complete their component writers before the synchronous gather.
            EntityManager.CompleteDependencyBeforeRO<ShieldState>(); EntityManager.CompleteDependencyBeforeRO<ShieldRipple>(); EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            count.Value = 0;
            using (Gather.Auto()) new ShieldGatherJob { Output = batch.Data, Count = count, Cap = Rig.Budget.MaxShields, Tick = SystemAPI.GetSingleton<SimClock>().Tick, RippleSeconds = Rig.Config.ShieldRippleSeconds, FlashSeconds = Rig.Config.ShieldHitFlashSeconds, Color = Rig.DefaultColor(FxKind.ShieldShell) }.Run();
            batch.Count = count.Value; using (Draw.Auto()) batch.Draw(); Rig.Stats.ShieldsDrawn = count.Value;
        }
        protected override void OnDestroy() { Dependency.Complete(); count.Dispose(); }
    }
}
