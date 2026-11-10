using Game.Simulation.Extraction;
using Game.Tests.EditMode.Simulation.Hazards;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Extraction
{
    /// <summary>EXT-002 (SPEC-93 §4): the magnet tow. Bodies move by hand (the zone fixture), so only the cable's impulses act.</summary>
    public sealed class TowTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(DamageStreamsSystem), typeof(TowSystem), typeof(ImpulseApplySystem) };

        NoiseEvent[] Noise()
        {
            var n = world.GetSingleton<NoiseEvents>().Current;
            var a = new NoiseEvent[n.Length]; for (int i = 0; i < n.Length; i++) a[i] = n[i]; return a;
        }

        [Test] public void Slack_AppliesNothing()
        {
            var ship = Body(new float2(0, 0), new float2(0, 5)); var load = Body(new float2(0, -20), float2.zero, 50000);
            Assert.That(TowApi.Attach(EM, ship, load, 30, Tick), Is.True);
            Step(10);
            Assert.That(Velocity(load), Is.EqualTo(float2.zero));
            Assert.That(Velocity(ship).y, Is.EqualTo(5).Within(1e-4f));
            Assert.That(EM.GetComponentData<TowLink>(ship).Tension, Is.EqualTo(0));
        }

        [Test] public void Taut_PullsBothAndConservesMomentum()
        {
            float ms = 20000, ml = 60000;
            var ship = Body(new float2(0, 0), new float2(0, 10), ms); var load = Body(new float2(0, -20), float2.zero, ml);
            TowApi.Attach(EM, ship, load, 20, Tick);
            float p0 = ms * 10;
            Step(240);
            float vs = Velocity(ship).y, vl = Velocity(load).y;
            Assert.That(vl, Is.GreaterThan(1), "the load is pulled along");
            Assert.That(vs, Is.LessThan(10), "the load is heavy for the ship");
            Assert.That(ms * vs + ml * vl, Is.EqualTo(p0).Within(p0 * 1e-3f), "the cable only moves momentum between them");
            float gap = math.distance(Position(ship), Position(load));
            Assert.That(gap, Is.LessThan(30), "the cable holds the load near its length");
        }

        [Test] public void OverBreakForce_Snaps()
        {
            var ship = Body(new float2(0, 0), new float2(0, 40)); var load = Body(new float2(0, -20), float2.zero, 1e6f);
            TowApi.Attach(EM, ship, load, 20, Tick, breakForce: 1e5f);
            bool snapped = false;
            for (int i = 0; i < 60 && !snapped; i++)
            {
                Step();
                foreach (var n in Noise()) snapped |= n.Kind == (byte)NoiseKind.Snap && n.Source == ship;
            }
            Assert.That(snapped, Is.True);
            Assert.That(EM.HasComponent<TowLink>(ship), Is.False);
            Assert.That(EM.HasComponent<TowLoad>(load), Is.False);
        }

        [Test] public void SuddenStop_Knocks()
        {
            var ship = Body(new float2(0, 0), new float2(0, 6)); var load = Body(new float2(0, -20), new float2(0, 6), 30000);
            TowApi.Attach(EM, ship, load, 25, Tick);
            Step(5);
            Assert.That(Noise(), Is.Empty);
            var v = EM.GetComponentData<Unity.Physics.PhysicsVelocity>(load); v.Linear = float3.zero;   // it hit a wall
            EM.SetComponentData(load, v);
            Step();
            var noise = Noise();
            Assert.That(noise.Length, Is.EqualTo(1));
            Assert.That(noise[0].Kind, Is.EqualTo((byte)NoiseKind.Knock));
            Assert.That(noise[0].Source, Is.EqualTo(ship), "the knock gives the tower away");
            Assert.That(noise[0].Radius, Is.GreaterThanOrEqualTo(TowSystem.MinKnockRadius));
            Step();
            Assert.That(Noise(), Is.Empty, "one knock, not one per tick");
        }

        [Test] public void LoadGone_Releases()
        {
            var ship = Body(new float2(0, 0), float2.zero); var load = Body(new float2(0, -20), float2.zero);
            TowApi.Attach(EM, ship, load, 25, Tick);
            EM.DestroyEntity(load);
            Step();
            Assert.That(EM.HasComponent<TowLink>(ship), Is.False);
        }

        [Test] public void OneCablePerLoad_AndRelease()
        {
            var a = Body(new float2(0, 0), float2.zero); var b = Body(new float2(10, 0), float2.zero); var load = Body(new float2(0, -20), float2.zero);
            Assert.That(TowApi.Attach(EM, a, load, 25, Tick), Is.True);
            Assert.That(TowApi.Attach(EM, b, load, 25, Tick), Is.False);
            TowApi.Release(EM, a);
            Assert.That(EM.HasComponent<TowLoad>(load), Is.False);
            Assert.That(TowApi.Attach(EM, b, load, 25, Tick), Is.True);
        }
    }
}
