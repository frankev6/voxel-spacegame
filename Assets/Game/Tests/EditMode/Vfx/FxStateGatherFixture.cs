using Game.Core.Components;
using Game.Data.Definitions;
using Game.Presentation.Vfx;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
namespace Game.Tests.EditMode.Vfx
{
    [DisableAutoCreation] public partial class FxGatherLookups : SystemBase
    {
        protected override void OnUpdate() { }
        internal void Run(BeamGatherJob job, EntityQuery query) => job.Run(query);
        internal void Run(PlumeGatherJob job, EntityQuery query) => job.Run(query);
        internal void Run(ShieldGatherJob job, EntityQuery query) => job.Run(query);
        internal ComponentLookup<T> Lookup<T>() where T : unmanaged, IComponentData => GetComponentLookup<T>(true);
    }
    internal sealed class FxStateGatherFixture : System.IDisposable
    {
        internal readonly World World = new World("VFX gather");
        internal EntityManager EM => World.EntityManager;
        internal NativeReference<int> Count = new NativeReference<int>(Allocator.Persistent);
        internal readonly NativeArray<BeamInstance> Beams = new NativeArray<BeamInstance>(8, Allocator.Persistent);
        internal readonly NativeArray<PlumeInstance> Plumes = new NativeArray<PlumeInstance>(8, Allocator.Persistent);
        internal readonly NativeArray<ShieldInstance> Shields = new NativeArray<ShieldInstance>(8, Allocator.Persistent);
        internal readonly FxUploadFixture Upload = new FxUploadFixture(FxKind.Beam);
        internal Entity Grid;
        internal FxStateGatherFixture() { Grid = EM.CreateEntity(typeof(LocalToWorld)); EM.SetComponentData(Grid, new LocalToWorld { Value = float4x4.identity }); }
        internal Entity Functional<T>(T state) where T : unmanaged, IComponentData
        { var entity = EM.CreateEntity(typeof(FunctionalBlock), typeof(T)); EM.SetComponentData(entity, new FunctionalBlock { Grid = Grid }); EM.SetComponentData(entity,state); return entity; }
        internal void Beam(uint frame = 0)
        {
            Count.Value = 0; using var query = EM.CreateEntityQuery(typeof(BeamState), typeof(FunctionalBlock));
            var job = new BeamGatherJob { Transforms = World.GetOrCreateSystemManaged<FxGatherLookups>().Lookup<LocalToWorld>(), Slots = Upload.Data.Slots,
                Hashes = Upload.Data.Hashes, Output = Beams, Count = Count, Impacts = Upload.PendingImpacts, Cap = 8, DefaultSlot = 1, Frame = frame, Width = .4f }; World.GetOrCreateSystemManaged<FxGatherLookups>().Run(job, query);
        }
        internal void Plume()
        {
            Count.Value = 0; using var query = EM.CreateEntityQuery(typeof(ThrusterState), typeof(ThrusterVisual), typeof(FunctionalBlock)); var lookups = World.GetOrCreateSystemManaged<FxGatherLookups>();
            var job = new PlumeGatherJob { Transforms = lookups.Lookup<LocalToWorld>(), Hulks = lookups.Lookup<Hulk>(), Output = Plumes, Count = Count, Cap = 8, Minimum = .02f, Length = 6, Color = new float4(1) }; World.GetOrCreateSystemManaged<FxGatherLookups>().Run(job, query);
        }
        internal void Shield(uint tick = 120)
        {
            Count.Value = 0; using var query = EM.CreateEntityQuery(typeof(ShieldState), typeof(LocalToWorld), typeof(ShieldRipple));
            var job = new ShieldGatherJob { Output = Shields, Count = Count, Cap = 8, Tick = tick, RippleSeconds = .6f, FlashSeconds = .15f, Color = new float4(1) }; World.GetOrCreateSystemManaged<FxGatherLookups>().Run(job, query);
        }
        public void Dispose() { World.Dispose(); Count.Dispose(); Beams.Dispose(); Plumes.Dispose(); Shields.Dispose(); Upload.Dispose(); }
    }
}
