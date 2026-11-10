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
    /// <summary>EXT-005 (SPEC-93 decisions 6, 7): watchers, codes, noise and Wanted. No physics world: sight lines are clear.</summary>
    public sealed class WatchTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(DamageStreamsSystem), typeof(TowSystem), typeof(CoreTetherSystem), typeof(WatchSystem), typeof(ImpulseApplySystem) };
        const byte Alliance = 4;

        Entity Light(float3 at, float3 facing, float range = 300, float halfArcDeg = 25, ushort district = 0)
        {
            var e = EM.CreateEntity(typeof(LocalTransform), typeof(Watcher));
            EM.SetComponentData(e, LocalTransform.FromPosition(at));
            EM.SetComponentData(e, new Watcher { Kind = WatcherKind.Light, Faction = Alliance, Facing = facing, Range = range,
                CosHalfArc = math.cos(math.radians(halfArcDeg)), District = district });
            return e;
        }

        Entity Crew(float2 at, bool on, AccessCode code)
        {
            var ship = Body(at, float2.zero);
            TransponderApi.Set(EM, ship, on, code);
            return ship;
        }

        Suspicion S(Entity e) => EM.GetComponentData<Suspicion>(e);

        [Test] public void DarkShipInTheCone_BecomesWanted()
        {
            Light(float3.zero, new float3(0, 0, 1));
            var ship = Crew(new float2(0, 100), false, AccessCode.None);
            Step(30);
            Assert.That(S(ship).Value, Is.GreaterThan(.2f)); Assert.That(S(ship).Seen, Is.EqualTo(1));
            Assert.That(EM.HasComponent<Wanted>(ship), Is.False);
            Step(120);
            Assert.That(TransponderApi.IsWantedBy(EM, ship, Alliance), Is.True);
        }

        [Test] public void OutOfTheCone_Unseen()
        {
            Light(float3.zero, new float3(0, 0, 1));
            var ship = Crew(new float2(100, 0), false, AccessCode.None);
            Step(120);
            Assert.That(S(ship).Value, Is.EqualTo(0));
        }

        [Test] public void RightCodeInItsZone_Fits_WrongCodeDoesNot()
        {
            Light(float3.zero, new float3(0, 0, 1));
            var zone = EM.CreateEntity(typeof(LocalTransform), typeof(AccessZone));
            EM.SetComponentData(zone, LocalTransform.FromPosition(new float3(0, 0, 100)));
            EM.SetComponentData(zone, new AccessZone { HalfExtents = new float3(60, 50, 60), Needs = AccessCode.Work });
            var worker = Crew(new float2(-10, 100), true, AccessCode.Work);
            var trader = Crew(new float2(10, 100), true, AccessCode.Trader);
            var outside = Crew(new float2(0, 200), true, AccessCode.Trader);
            Step(60);
            Assert.That(S(worker).Value, Is.EqualTo(0), "a work code in the works fits");
            Assert.That(S(trader).Value, Is.GreaterThan(0), "a trader in the works does not");
            Assert.That(S(outside).Value, Is.EqualTo(0), "a trader outside every zone is traffic");
        }

        [Test] public void DarkDistrict_LightSeesNothing()
        {
            var d = EM.CreateEntity(typeof(District)); EM.SetComponentData(d, new District { Id = 3, Powered = 0 });
            Light(float3.zero, new float3(0, 0, 1), district: 3);
            var ship = Crew(new float2(0, 100), false, AccessCode.None);
            Step(120);
            Assert.That(S(ship).Value, Is.EqualTo(0));
        }

        [Test] public void HeardKnock_RaisesTheTower()
        {
            Light(new float3(0, 0, -40), new float3(0, 0, -1));   // looking away
            var ship = Crew(new float2(0, 0), true, AccessCode.Trader);
            var load = Body(new float2(0, -20), new float2(0, 6), 30000);
            var v = EM.GetComponentData<PhysicsVelocity>(ship); v.Linear = new float3(0, 0, 6); EM.SetComponentData(ship, v);
            TowApi.Attach(EM, ship, load, 25, Tick);
            Step(5);
            Assert.That(S(ship).Value, Is.EqualTo(0));
            EM.SetComponentData(load, new PhysicsVelocity());   // it hit something
            Step();
            Assert.That(S(ship).Heard, Is.EqualTo(1));
            Assert.That(S(ship).Value, Is.EqualTo(WatchRules.HeardJump).Within(1e-4f));
        }

        [Test] public void Unseen_Fades()
        {
            var light = Light(float3.zero, new float3(0, 0, 1));
            var ship = Crew(new float2(0, 100), false, AccessCode.None);
            Step(30);
            float peak = S(ship).Value;
            EM.DestroyEntity(light);
            Step((int)(60 * (WatchRules.DecayDelaySeconds + 2)));
            Assert.That(S(ship).Value, Is.LessThan(peak));
        }
    }
}
