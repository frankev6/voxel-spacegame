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
    public sealed class PlumeRenderTests
    {
        static Entity Thruster(FxStateGatherFixture f, float intensity)
        { var e = f.Functional(new ThrusterState { Intensity = intensity }); f.EM.AddComponentData(e, new ThrusterVisual { NozzleLocal = new float3(0,0,-1), ExhaustDirLocal = new float3(0,0,-1), NozzleRadius = .75f }); return e; }
        [Test] public void IntensityZero_NoInstance()
        { using var f = new FxStateGatherFixture(); Thruster(f,0); f.Plume(); Assert.AreEqual(0,f.Count.Value); }
        [Test] public void LengthScalesWithIntensity()
        { using var f = new FxStateGatherFixture(); Thruster(f,.5f); f.Plume(); Assert.AreEqual(1,f.Count.Value); Assert.AreEqual(3,f.Plumes[0].Length); Assert.AreEqual(.5f,f.Plumes[0].Color.w); }
        [Test] public void FollowsGridRotation()
        {
            using var f = new FxStateGatherFixture(); Thruster(f,1); f.EM.SetComponentData(f.Grid, new LocalToWorld { Value = float4x4.TRS(new float3(10,0,0), quaternion.RotateY(math.PI/2),new float3(1)) }); f.Plume();
            Assert.Less(math.distance(new float3(-1,0,0),f.Plumes[0].Dir),.0001); Assert.Less(math.distance(new float3(9,0,0),f.Plumes[0].Nozzle),.0001);
        }
        [Test] public void Hulk_NoPlume()
        { using var f = new FxStateGatherFixture(); Thruster(f,1); f.EM.AddComponent<Hulk>(f.Grid); f.Plume(); Assert.AreEqual(0,f.Count.Value); }
    }
}
