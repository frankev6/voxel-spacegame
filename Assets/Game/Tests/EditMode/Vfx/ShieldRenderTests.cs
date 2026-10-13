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
    public sealed class ShieldRenderTests
    {
        static ShieldState State => new ShieldState { Active = 1, Hp = 50, MaxHp = 100, Center = new float3(1,2,3), Radii = new float3(2,3,4) };
        static void Add(FxStateGatherFixture f, ShieldState state) { f.EM.AddComponentData(f.Grid,state); f.EM.AddBuffer<ShieldRipple>(f.Grid); }
        [Test] public void Inactive_NotDrawn()
        { using var f = new FxStateGatherFixture(); var state = State; state.Active = 0; Add(f,state); f.Shield(); Assert.AreEqual(0,f.Count.Value); }
        [Test] public void RippleAges_PerSpec()
        {
            using var f = new FxStateGatherFixture(); Add(f,State); var b = f.EM.GetBuffer<ShieldRipple>(f.Grid);
            b.Add(new ShieldRipple { LocalPoint = new float3(3,2,3), StartTick = 114, Strength = 1 }); b.Add(new ShieldRipple { StartTick = 1, Strength = 1 }); f.Shield();
            Assert.AreEqual(.1f,f.Shields[0].Ripple0.w,.00001); Assert.AreEqual(new float3(1,0,0),f.Shields[0].Ripple0.xyz); Assert.AreEqual(-1,f.Shields[0].Ripple1.w);
            Assert.AreEqual(1f/3,f.Shields[0].Flash,.00001);
        }
        [Test] public void EllipsoidMatrix_MapsUnitSphere()
        { using var f = new FxStateGatherFixture(); Add(f,State); f.Shield(); Assert.AreEqual(new float3(3,2,3),math.transform(f.Shields[0].EllipsoidToWorld,new float3(1,0,0))); Assert.AreEqual(.5f,f.Shields[0].HpFraction); }
    }
}
