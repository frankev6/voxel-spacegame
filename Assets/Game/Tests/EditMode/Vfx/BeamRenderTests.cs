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
    public sealed class BeamRenderTests
    {
        [Test] public void End_IsClippedHitPoint()
        {
            using var f = new FxStateGatherFixture(); f.Functional(new BeamState { Active = 1, MuzzleLocal = new float3(1,2,3), EndWorld = new float3(12,4,8) }); f.Beam();
            Assert.AreEqual(1, f.Count.Value); Assert.AreEqual(new float3(12,4,8), f.Beams[0].End); Assert.AreEqual(new float3(1,2,3), f.Beams[0].Start);
        }
        [Test] public void DestroyedGrid_NotDrawn()
        { using var f = new FxStateGatherFixture(); f.Functional(new BeamState { Active = 1 }); f.EM.DestroyEntity(f.Grid); f.Beam(); Assert.AreEqual(0, f.Count.Value); }
        [Test] public void ImpactSparks_EverySixthFrame()
        {
            using var f = new FxStateGatherFixture(); f.Functional(new BeamState { Active = 1, HitSomething = 1, EndWorld = new float3(10,0,0) });
            for (uint i = 0; i < 60; i++) f.Beam(i); Assert.AreEqual(10, f.Upload.PendingImpacts.Length); Assert.AreEqual(new float3(10,0,0), f.Upload.PendingImpacts[0].Pos);
        }
    }
}
