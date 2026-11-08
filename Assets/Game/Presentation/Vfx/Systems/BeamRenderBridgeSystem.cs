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
    /// <summary>SPEC-63 state-driven beams presentation; bounded session-owned buffers.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(FxUploadSystem))]
    public partial class BeamRenderBridgeSystem : SystemBase, IFxRigConsumer
    {
        static readonly ProfilerMarker Gather = new ProfilerMarker(ProfilerCategory.Scripts, "BeamRenderBridgeSystem.Gather");
        static readonly ProfilerMarker Draw = new ProfilerMarker(ProfilerCategory.Scripts, "BeamRenderBridgeSystem.Draw");
        public FxRig Rig { get; set; }
        NativeReference<int> count;
        ComponentLookup<LocalToWorld> transforms; uint frame;
        protected override void OnCreate() { RequireForUpdate<GameplayActive>(); count = new NativeReference<int>(Allocator.Persistent); transforms = GetComponentLookup<LocalToWorld>(true); }
        protected override void OnUpdate()
        {
            var batch = Rig?.Beams; if (batch == null) return; batch.Begin();
            // Presentation reads after fixed-step producers; complete their component writers before the synchronous gather.
            EntityManager.CompleteDependencyBeforeRO<BeamState>(); EntityManager.CompleteDependencyBeforeRO<FunctionalBlock>(); EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            count.Value = 0; transforms.Update(this); frame++;
            using (Gather.Auto()) new BeamGatherJob { Output = batch.Data, Count = count, Cap = Rig.Budget.MaxBeams, Transforms = transforms, Slots = Rig.Slots, Hashes = Rig.SlotByHash, Impacts = Rig.PendingImpacts, DefaultSlot = Rig.DefaultSlotByKind[(int)FxKind.Beam], Frame = frame, Width = Rig.Config.BeamDefaultWidth }.Run();
            // WPN-019: charge beams share the beam batch and budget, after the plain beams.
            if (SystemAPI.TryGetSingleton<WeaponDefsRef>(out var weapons) && weapons.Value.IsCreated)
            {
                EntityManager.CompleteDependencyBeforeRO<ChargeBeamState>();
                int slot = Rig.DefaultSlotByKind[(int)FxKind.Beam];
                var fallback = slot >= 0 ? FxStateMath.Linear(Rig.Slots[slot].DefaultColorRgba8) : new Unity.Mathematics.float4(1);
                using (Gather.Auto()) new ChargeBeamGatherJob { Output = batch.Data, Count = count, Cap = Rig.Budget.MaxBeams, Transforms = transforms,
                    Weapons = weapons.Value, Impacts = Rig.PendingImpacts, Frame = frame, Time = (float)SystemAPI.Time.ElapsedTime, FallbackColor = fallback }.Run();
            }
            batch.Count = count.Value; using (Draw.Auto()) batch.Draw(); Rig.Stats.BeamsDrawn = count.Value;
        }
        protected override void OnDestroy() { Dependency.Complete(); count.Dispose(); }
    }
}
