using Game.Core;
using Game.Data.Definitions;
using Game.Simulation.Hazards;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Hazards
{
    /// <summary>HAZ-017: engine wash (ADR-0119 decision 5) pushes what is behind a firing engine, never its own ship.</summary>
    [Category("Scenario")]
    public sealed class EngineWashTests
    {
        static ShipBlueprintDefinition Blueprint(CombatFixture f, string id) => f.Flight.Catalog.Get<ShipBlueprintDefinition>(new ContentId(id));

        static Aabb Box(CombatFixture f, Entity root)
        {
            var local = f.Em.GetComponentData<PhysicsCollider>(root).Value.Value.CalculateAabb(); var t = f.Flight.Transform(root);
            return new Aabb { Min = t.TransformPoint(local.Min), Max = t.TransformPoint(local.Max) };
        }

        /// <summary>The Needle (Vestari racer, two Sprint M and a Sprint S astern) at full forward thrust; a WRAITH 30 m behind its stern.
        /// Returns the summed external push on the WRAITH over half a second.</summary>
        static float3 Tailgate(bool wash)
        {
            using var f = wash ? new CombatFixture(false, typeof(EngineWashSystem)) : new CombatFixture(false);
            var leader = f.Flight.SpawnBlueprint(Blueprint(f, "ship_racer_needle"), new float3(0, 0, 0));
            // TODO(batch-5): re-measure on WRAITH; the -0.05 m/s push floor was measured on the lighter retired Fighter.
            var tail = f.Flight.SpawnBlueprint(Blueprint(f, "ship_nocturne_wraith"), new float3(0, 0, -80));
            f.Ready(leader, tail);
            float stern = Box(f, leader).Min.z; var tailBox = Box(f, tail);
            var pose = f.Flight.Transform(tail); pose.Position.z += stern - 30 - tailBox.Max.z; f.Em.SetComponentData(tail, pose);
            f.Flight.Command(leader, thrust: new float2(0, 1));
            float3 pushed = 0; uint seen = f.Em.GetComponentData<ExternalPush>(tail).Tick;
            for (int i = 0; i < 30; i++)
            {
                f.Tick();
                var push = f.Em.GetComponentData<ExternalPush>(tail);
                if (push.Tick != seen) { pushed += push.LinearDv; seen = push.Tick; }
            }
            return pushed;
        }

        [Test] public void Tailgater_Pushed()
        {
            var with = Tailgate(true); var without = Tailgate(false);
            TestContext.WriteLine($"Tailgater push over 0.5 s: with wash {with}, without {without}");
            System.IO.Directory.CreateDirectory("Logs"); System.IO.File.WriteAllText("Logs/engine-wash.txt", $"tailgater dv over 0.5 s: {with} (without wash {without})\n");
            Assert.Less(with.z, -.05f, "the WRAITH 30 m behind the Needle's stern is pushed back");
            Assert.AreEqual(0, without.z, 1e-6f, "nothing else pushes it");
            Assert.Less(math.abs(with.x), math.abs(with.z), "the push runs along the exhaust");
        }

        static (float3 v, float3 p) Run(bool wash)
        {
            using var f = wash ? new CombatFixture(false, typeof(EngineWashSystem)) : new CombatFixture(false);
            var ship = f.Flight.SpawnBlueprint(Blueprint(f, "ship_racer_needle"), new float3(0, 0, 0));
            f.Ready(ship);
            f.Flight.Command(ship, thrust: new float2(0, 1));
            f.Tick(60);
            return (f.Flight.Velocity(ship).Linear, f.Flight.Transform(ship).Position);
        }

        [Test] public void Owner_Unaffected()
        {
            var with = Run(true); var without = Run(false);
            Assert.AreEqual(without.v, with.v, "a ship never feels its own wash");
            Assert.AreEqual(without.p, with.p);
        }
    }
}
