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
    public sealed class ChunkDissolveTests
    {
        [Test] public void Value_FollowsTimeLeft()
        {
            using var w = Game.Tests.Common.EcsTestWorld.Create(typeof(ChunkDissolveSyncSystem)); var em = w.EntityManager; w.AddGameplayActive(); w.AddSimClock();
            em.CreateSingleton(new VfxRuntimeConfig { ChunkDissolveSeconds = 2 });
            var entity = em.CreateEntity(typeof(ChunkBody), typeof(ChunkDissolve)); em.SetComponentData(entity,new ChunkBody { Live = 1, EndTick = 240 });
            using var clock = em.CreateEntityQuery(typeof(SimClock));
            foreach (uint tick in new uint[] { 1,120,180,240 })
            { var time = clock.GetSingleton<SimClock>(); time.Tick = tick; clock.SetSingleton(time); w.World.Unmanaged.GetExistingUnmanagedSystem<ChunkDissolveSyncSystem>().Update(w.World.Unmanaged); em.CompleteAllTrackedJobs(); Assert.AreEqual(math.saturate(1-(240-tick)/120f),em.GetComponentData<ChunkDissolve>(entity).Value,.0001); }
        }
    }
}
