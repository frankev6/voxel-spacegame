using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Combat
{
    /// <summary>HAZ-005: the SPEC-91 §8 feel table, measured. Real reference ships take one shot of each authored weapon;
    /// the Δv is read from <see cref="ExternalPush"/> on the first tick the hit lands (flight assist on, fresh ships).</summary>
    [Category("Scenario")]
    public sealed class HitForceFeelTests
    {
        const string Plasma = "weapon_plasma_turret_small", Railgun = "weapon_railgun_medium", Siege = "weapon_siege_cannon_heavy";

        internal struct Row { public string Ship, Shot; public float Dv, YawDegPerSec, Mass; public bool Impact; }
        internal struct Recoil { public string Weapon; public float Impulse, ShooterMass; }

        static readonly string[] Ships = { "ship_fighter", "ship_nocturne_wraith", "ship_nocturne_requiem" };

        [Test] public void Table_WithinTargets()
        {
            var rows = new List<Row>(); var recoils = new List<Recoil>();
            foreach (var ship in Ships) Measure(ship, rows, recoils);
            float fighterMass = rows.Find(r => r.Ship == "ship_fighter").Mass;

            var table = new StringBuilder("ship,shot,mass_kg,dv_mps,yaw_dps,impact_event\n");
            foreach (var r in rows) table.AppendLine($"{r.Ship},{r.Shot},{r.Mass:F0},{r.Dv:F3},{r.YawDegPerSec:F1},{r.Impact}");
            foreach (var r in recoils) table.AppendLine($"recoil,{r.Weapon},{r.ShooterMass:F0},{r.Impulse / r.ShooterMass:F3},fighter-equivalent {r.Impulse / fighterMass:F3}");
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/hitforce-table.csv", table.ToString());
            UnityEngine.Debug.Log("HAZ-005 hit-force table\n" + table);

            // A centre hit carries exactly the shot's momentum (projectileMass × speed × knockbackScale 1): plasma 22.5 kg × 400 m/s.
            foreach (var r in rows) if (r.Shot.StartsWith("small plasma")) Assert.AreEqual(22.5f * 400 / r.Mass, r.Dv, .05f * r.Dv, r.Ship);
            // Recoil is the momentum × recoilScale 0.17 (plasma 9 000 N·s, railgun 50 kg × 1 200 m/s).
            Assert.AreEqual(22.5f * 400 * .17f, recoils.Find(r => r.Weapon == "small plasma").Impulse, 80);
            Assert.AreEqual(50f * 1200 * .17f, recoils.Find(r => r.Weapon == "railgun").Impulse, 500);
            // Owner-approved feel (2026-10-17, "feels right"): the measured SPEC-91 §8 table is the baseline, ±15%.
            foreach (var r in rows)
            {
                float baseline = Baseline(r.Ship, r.Shot);
                Assert.AreEqual(baseline, r.Dv, .15f * baseline, $"{r.Shot} on {r.Ship}: SPEC-91 §8 records {baseline} m/s");
            }
            Assert.GreaterOrEqual(rows.Find(r => r.Ship == "ship_fighter" && r.Shot.StartsWith("railgun")).YawDegPerSec, 20, "A bow railgun hit kicks a fighter's nose round");
            // The camera shake feed: hard shoves raise an Impact event, light ones do not.
            foreach (var r in rows)
            {
                if (r.Dv >= ExternalPushFeedbackSystem.MinDv * 1.05f) Assert.IsTrue(r.Impact, $"{r.Shot} on {r.Ship} ({r.Dv} m/s) must raise an Impact event");
                if (r.Dv <= ExternalPushFeedbackSystem.MinDv * .95f) Assert.IsFalse(r.Impact, $"{r.Shot} on {r.Ship} ({r.Dv} m/s) is too light for an Impact event");
            }
            Assert.IsTrue(rows.Find(r => r.Ship == "ship_fighter" && r.Shot.StartsWith("railgun")).Impact, "A railgun hit on the starter fighter must feed the camera shake");
        }

        static float Baseline(string ship, string shot)
        {
            int s = System.Array.IndexOf(Ships, ship), w = shot.StartsWith("small plasma") ? 0 : shot.StartsWith("railgun") ? 1 : 2;
            float[,] table = { { .366f, 2.44f, 1.68f }, { .128f, .853f, .70f }, { .023f, .152f, .101f } };
            return table[s, w];
        }

        static void Measure(string shipId, List<Row> rows, List<Recoil> recoils)
        {
            using var f = new CombatFixture(true, typeof(RecoilSystem), typeof(ExternalPushFeedbackSystem));
            var catalog = f.Flight.Catalog;
            // The combat fixture's test tuning uses 2 N·s per damage; measure with the shipped config_weapons value.
            using (var tuningQuery = f.Em.CreateEntityQuery(typeof(WeaponTuning)))
            {
                var tuning = tuningQuery.GetSingleton<WeaponTuning>();
                tuning.ExplosionImpulsePerDamage = catalog.Get<WeaponTuningDefinition>(new ContentId("config_weapons")).ExplosionImpulsePerDamage;
                tuningQuery.SetSingleton(tuning);
            }

            var target = f.Flight.SpawnBlueprint(catalog.Get<ShipBlueprintDefinition>(new ContentId(shipId)));
            f.Flight.Ready(target);
            var box = WorldBox(f.Em, target); float3 c = Centre(f.Em, target);
            // Plasma (±5° traverse) and the railgun (±30°) sit astern facing +z; the siege cannon fires across from the port side.
            var plasma = f.Ship(Plasma, new float3(c.x, 0, box.Min.z - 40));
            var railgun = f.Ship(Railgun, new float3(c.x - 20, 0, math.lerp(c.z, box.Max.z, .6f) - 60));   // 18° inside its ±30° traverse
            var siege = f.Ship(Siege, new float3(box.Min.x - 45, 0, c.z));   // beam-on: a 90° turn, inside its ±150° traverse
            f.Ready(target, plasma, railgun, siege);
            float mass = 1f / f.Em.GetComponentData<PhysicsMass>(target).InverseMass;

            rows.Add(Shoot(f, shipId, "small plasma, centre", plasma, target, () => Centre(f.Em, target), recoils, mass));
            rows.Add(Shoot(f, shipId, "railgun, bow", railgun, target,
                () => { var b = WorldBox(f.Em, target); var m = Centre(f.Em, target); return new float3(m.x, m.y, math.lerp(m.z, b.Max.z, .6f)); }, recoils, mass));
            rows.Add(Shoot(f, shipId, "siege, direct", siege, target, () => Centre(f.Em, target), null, mass));
        }

        static Row Shoot(CombatFixture f, string ship, string shot, Entity shooter, Entity target, System.Func<float3> aim, List<Recoil> recoils, float mass)
        {
            uint seen = f.Em.GetComponentData<ExternalPush>(target).Tick, shooterSeen = f.Em.GetComponentData<ExternalPush>(shooter).Tick;
            bool recoilDone = recoils == null || recoils.Exists(r => r.Weapon == WeaponOf(shot));
            // Hold the trigger until one shot leaves: turrets traverse first (the siege cannon turns 180° at 45°/s).
            var fired = f.Store.Stats.Value.Fired;
            for (int tick = 0; f.Store.Stats.Value.Fired == fired; tick++)
            {
                Assert.Less(tick, 600, $"{shot} never fired");
                f.Fire(shooter, aim(), target); f.Tick();
            }
            f.Stop(shooter);
            for (int tick = 0; tick < 240; tick++)
            {
                var own = f.Em.GetComponentData<ExternalPush>(shooter);
                if (!recoilDone && own.Tick != shooterSeen && math.length(own.LinearDv) > 1e-5f)
                {
                    float shooterMass = 1f / f.Em.GetComponentData<PhysicsMass>(shooter).InverseMass;
                    recoils.Add(new Recoil { Weapon = WeaponOf(shot), Impulse = math.length(own.LinearDv) * shooterMass, ShooterMass = shooterMass });
                    recoilDone = true;
                }
                var push = f.Em.GetComponentData<ExternalPush>(target);
                if (push.Tick != seen && math.length(push.LinearDv) > 1e-5f)
                {
                    // A shell's blast can land on the tick after its impact: sum the pushes of the hit and the next three ticks.
                    float3 dv = push.LinearDv; float yaw = push.YawDv; uint last = push.Tick;
                    for (int after = 0; after < 3; after++)
                    {
                        f.Tick(); push = f.Em.GetComponentData<ExternalPush>(target);
                        if (push.Tick != last) { dv += push.LinearDv; yaw += push.YawDv; last = push.Tick; }
                    }
                    var row = new Row { Ship = ship, Shot = shot, Dv = math.length(dv.xz), YawDegPerSec = math.degrees(math.abs(yaw)), Mass = mass,
                        Impact = DrainImpact(f, Centre(f.Em, target)) };
                    f.Tick(30); return row;
                }
                f.Tick();
            }
            var box = WorldBox(f.Em, target);
            Assert.Fail($"{shot} never reached {ship}: aim {aim()}, box {box.Min}..{box.Max}, muzzle {f.Muzzle(shooter)}, hits {f.Store.Stats.Value.Hits}"); return default;
        }

        /// <summary>True when an Impact event (the camera-shake feed) was raised at the target; clears the test world's FX queue.</summary>
        static bool DrainImpact(CombatFixture f, float3 at)
        {
            var fx = f.World.GetSingleton<Game.Voxel.PresentationStreams.FxEventQueue>();
            fx.Access.Exclusive.Value.Complete(); fx.Access.Readers.Value.Complete();
            bool found = false;
            using (var events = fx.Queue.ToArray(Unity.Collections.Allocator.Temp))
                foreach (var e in events) found |= e.Kind == FxKind.Impact && math.distance(e.Pos, at) < 25 && e.Scale >= ExternalPushFeedbackSystem.MinDv;
            fx.Queue.Clear(); return found;
        }

        static string WeaponOf(string shot) => shot.StartsWith("small plasma") ? "small plasma" : shot.StartsWith("railgun") ? "railgun" : "siege";

        static float3 Centre(EntityManager em, Entity root)
            => em.GetComponentData<LocalTransform>(root).TransformPoint(em.GetComponentData<PhysicsMass>(root).CenterOfMass);

        static Aabb WorldBox(EntityManager em, Entity root)
        {
            var local = em.GetComponentData<PhysicsCollider>(root).Value.Value.CalculateAabb();
            var t = em.GetComponentData<LocalTransform>(root);
            return new Aabb { Min = t.TransformPoint(local.Min), Max = t.TransformPoint(local.Max) };
        }
    }
}
