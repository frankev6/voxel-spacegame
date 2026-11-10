using Game.Simulation.Extraction;
using Game.Simulation.Mechanisms;
using Game.Tests.EditMode.Simulation.Hazards;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Extraction
{
    /// <summary>EXT-008 (SPEC-93 decision 10): the socket takes a free core and powers its den.</summary>
    public sealed class CoreSocketTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(DamageStreamsSystem), typeof(TowSystem), typeof(CoreTetherSystem), typeof(CoreSocketSystem), typeof(ImpulseApplySystem), typeof(CoreHoldSystem) };

        [Test] public void FreeCoreInRange_Installs_AndPowersTheDen()
        {
            var socket = EM.CreateEntity(typeof(LocalTransform), typeof(CoreSocket));
            EM.SetComponentData(socket, LocalTransform.FromPosition(new float3(0, 0, 100)));
            EM.SetComponentData(socket, new CoreSocket { Radius = 15, Den = 1 });
            var gun = EM.CreateEntity(typeof(DenPowered)); EM.SetComponentData(gun, new DenPowered { Den = 1 });
            var other = EM.CreateEntity(typeof(DenPowered)); EM.SetComponentData(other, new DenPowered { Den = 2 });
            var ship = Body(new float2(0, 40), float2.zero);
            var core = Body(new float2(0, 60), float2.zero, 80000);
            EM.AddComponentData(core, new StationCore { Freed = 0, Tethers = 1, MassT = 80 });
            TowApi.Attach(EM, ship, core, 30, Tick);
            var p = EM.GetComponentData<LocalTransform>(core); p.Position = new float3(0, 0, 92); EM.SetComponentData(core, p);
            Step();
            Assert.That(EM.GetComponentData<CoreSocket>(socket).Core, Is.EqualTo(Entity.Null), "a tethered core is not free");
            var c = EM.GetComponentData<StationCore>(core); c.Freed = 1; EM.SetComponentData(core, c);
            Step();
            Assert.That(EM.GetComponentData<CoreSocket>(socket).Core, Is.EqualTo(core));
            Assert.That(EM.GetComponentData<DenPowered>(gun).On, Is.EqualTo(1));
            Assert.That(EM.GetComponentData<DenPowered>(other).On, Is.EqualTo(0), "another den stays dark");
            Assert.That(TowApi.IsTowing(EM, ship), Is.False, "the cable lets go");
            Step(60);
            Assert.That(math.distance(Position(core), new float2(0, 100)), Is.LessThan(.5f), "held at the socket");
            Assert.That(CoreSocketSystem.DenHasPower(EM, 1), Is.True);
        }
    }
}
