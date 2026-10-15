using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Weapons;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.GraphicsIntegration;
using Unity.Profiling;
namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 §5.3: guarded projectile gather and one session-owned instanced draw.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(FxUploadSystem))]
    public partial class ProjectileTracerBridgeSystem : SystemBase, IFxRigConsumer
    {
        static readonly ProfilerMarker Gather = new ProfilerMarker(ProfilerCategory.Scripts, "ProjectileTracerBridgeSystem.Gather");
        static readonly ProfilerMarker Draw = new ProfilerMarker(ProfilerCategory.Scripts, "ProjectileTracerBridgeSystem.Draw");
        public FxRig Rig { get; set; }
        protected override void OnCreate() { RequireForUpdate<GameplayActive>(); RequireForUpdate<ProjectileStore>(); }
        protected override void OnUpdate()
        {
            var batch = Rig?.Tracers; if (batch == null) return; batch.Begin();
            var store = SystemAPI.GetSingleton<ProjectileStore>(); store.CompleteAccess();
            EntityManager.CompleteDependencyBeforeRO<OriginShiftRecord>();
            var record = SystemAPI.TryGetSingleton<OriginShiftRecord>(out var r) ? r : default;
            if (!TracerGatherJob.PendingShift(record, store.AppliedShiftSequence.Value, out var correction)) return;
            float alpha = 0;
            if (SystemAPI.TryGetSingletonBuffer<MostRecentFixedTime>(out var times, true) && times.Length > 0)
                alpha = (float)math.clamp(SystemAPI.Time.ElapsedTime - times[0].ElapsedTime, 0, 1.0 / 60);

            var config = Rig.Config; var colors = config.TracerColorByDamageType; int count = math.min(store.Rows.Length, Rig.Budget.MaxTracers);
            var scales = new NativeArray<float>(0, Allocator.TempJob);
            if (SystemAPI.TryGetSingleton<WeaponDefsRef>(out var defs) && defs.Value.IsCreated)
            {
                ref var weapons = ref defs.Value.Value.Weapons; scales.Dispose(); scales = new NativeArray<float>(weapons.Length, Allocator.TempJob);
                for (int w = 0; w < weapons.Length; w++) scales[w] = TracerGatherJob.Scale(weapons[w].ProjectileRadius);
            }
            using (scales)
            using (Gather.Auto()) new TracerGatherJob { Rows = store.Rows.AsArray(), WeaponScale = scales, Output = batch.Data, Correction = correction, Alpha = alpha,
                LengthSeconds = config.TracerLengthSeconds, MinLength = config.TracerMinLength, MaxLength = config.TracerMaxLength, Width = config.TracerWidth,
                Color0 = (UnityEngine.Vector4)colors[0].linear, Color1 = (UnityEngine.Vector4)colors[1].linear,
                Color2 = (UnityEngine.Vector4)colors[2].linear, Color3 = (UnityEngine.Vector4)colors[3].linear }.Schedule(count, 256).Complete();
            batch.Count = count; using (Draw.Auto()) batch.Draw(); Rig.Stats.TracersDrawn = count;
        }
    }
}
