using Game.Simulation.Extraction;
using Game.Tests.EditMode.Simulation.Hazards;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Extraction
{
    /// <summary>EXT-004 (SPEC-93 decision 5): tethers, blackouts and the freed core.</summary>
    public sealed class CoreTetherTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(DamageStreamsSystem), typeof(TowSystem), typeof(CoreTetherSystem), typeof(ImpulseApplySystem), typeof(CoreHoldSystem) };

        Entity holder, core;
        Entity[] tethers;

        void Build(int count = 2)
        {
            holder = EM.CreateEntity(typeof(LocalTransform));
            EM.SetComponentData(holder, LocalTransform.FromPosition(new float3(100, 0, 0)));
            core = Body(new float2(100, 10), float2.zero, 200000);
            EM.AddComponentData(core, new StationCore { Holder = holder, HoldLocal = new float3(0, 0, 10), HoldRotation = quaternion.identity, Tethers = (byte)count, MassT = 200 });
            tethers = new Entity[count];
            for (int i = 0; i < count; i++)
            {
                var d = EM.CreateEntity(typeof(District)); EM.SetComponentData(d, new District { Id = (ushort)(i + 1), Powered = 1 });
                tethers[i] = EM.CreateEntity(typeof(CoreTether));
                EM.SetComponentData(tethers[i], new CoreTether { Core = core, Holder = holder, Anchor = new float3(i * 10 - 5, 0, 0), District = (ushort)(i + 1) });
            }
            Step();
        }

        void Cut(Entity t) => world.GetSingleton<TetherCutRequests>().Pending.Add(t);
        bool Powered(ushort id)
        {
            using var q = EM.CreateEntityQuery(typeof(District));
            return Districts.Powered(EM, q, id);
        }
        StationCore Core => EM.GetComponentData<StationCore>(core);

        [Test] public void Cut_DarkensItsDistrict_LastFreesTheCore()
        {
            Build();
            Cut(tethers[0]); Step();
            Assert.That(Powered(1), Is.False); Assert.That(Powered(2), Is.True);
            Assert.That(Core.Tethers, Is.EqualTo(1)); Assert.That(Core.Freed, Is.EqualTo(0));
            bool loud = false; foreach (var n in world.GetSingleton<NoiseEvents>().Current) loud |= n.Kind == (byte)NoiseKind.Cut;
            Assert.That(loud, Is.True, "a cut is heard");
            uint at = Tick;
            Cut(tethers[1]); Step();
            Assert.That(Core.Freed, Is.EqualTo(1)); Assert.That(Core.FreedTick, Is.EqualTo(at));
            Assert.That(Powered(2), Is.False);
        }

        [Test] public void PostDestroyed_CutsItsTether()
        {
            Build(1);
            var post = EM.CreateEntity(typeof(LocalTransform));
            var t = EM.GetComponentData<CoreTether>(tethers[0]); t.Post = post; EM.SetComponentData(tethers[0], t);
            Step();
            Assert.That(Core.Freed, Is.EqualTo(0));
            EM.DestroyEntity(post); Step();
            Assert.That(EM.GetComponentData<CoreTether>(tethers[0]).Cut, Is.EqualTo(1));
            Assert.That(Core.Freed, Is.EqualTo(1));
        }

        [Test] public void HeldCore_SettlesBack_FreeCoreDoesNot()
        {
            Build();
            var p = EM.GetComponentData<LocalTransform>(core); p.Position = new float3(100, 0, 30); EM.SetComponentData(core, p);   // knocked 20 m off
            Step(60);
            Assert.That(math.distance(Position(core), new float2(100, 10)), Is.LessThan(.5f), "held at its pose");
            Cut(tethers[0]); Cut(tethers[1]); Step();
            EM.SetComponentData(core, new PhysicsVelocity { Linear = new float3(0, 0, 4) });
            Step(30);
            Assert.That(Velocity(core).y, Is.EqualTo(4).Within(1e-3f), "a free core drifts");
        }
    }
}
