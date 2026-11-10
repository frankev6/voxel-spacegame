using System.Collections;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Extraction;
using Game.Simulation.Extraction;
using Game.Tests.PlayMode.App;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Extraction
{
    /// <summary>SPEC-93 EXT-011: the extraction loop end to end in the playground, through the extraction command: the Rock builds and
    /// digs and takes a gun; Tollhouse Nine builds with its core on six tethers; cutting them frees the core and starts the alarm; the
    /// magnet takes the core; the core in the den's socket powers the den's gun and the job ends Installed; the den attack wave comes.</summary>
    public sealed class ExtractionLoopTests : SessionCommandFixture
    {
        DevCommandResult Run(string line)
        {
            var r = Host.Commands.Execute(line, Host.Context);
            Assert.AreNotEqual(DevResultKind.Failed, r.Kind, line + ": " + r.Text);
            return r;
        }

        [UnityTest] public IEnumerator Den_Heist_Tow_Socket_Attack()
        {
            Assume.That(LanternLayout.Pieces.Length, Is.GreaterThan(0), "the Lantern's layout is generated (tools/build-extraction.sh)");
            yield return RunCommand("playground");
            var x = Host.Playground.Extraction;
            Assert.IsNotNull(x, "the playground owns an extraction runtime");

            // the den: built on arrival, dug for ore, a gun on its face (dark until a core is socketed)
            Run("extraction goto=den@175,0,44,-90");
            yield return Until(() => x.Den.Built && x.Den.Rock != Entity.Null && EM.Exists(x.Den.Rock) && EM.HasComponent<VoxelGrid>(x.Den.Rock), "the Rock spawns", 30);
            Run("extraction goto=den@175,0,44,-90");   // again: the hauler (EXT-015) is still streaming in when the first teleport lands
            yield return new WaitForSeconds(2);
            StringAssert.StartsWith("digging", Run("extraction dig").Text);
            yield return Until(() => x.Den.Ore > 0, "a dig yields ore", 10);
            Run("extraction ore add=100");
            Run("extraction goto=den@180,10,50,-90");
            yield return new WaitForSeconds(.5f);
            StringAssert.StartsWith("gun placed", Run("extraction build gun").Text);

            // the Lantern: the core on six tethers; cut them all and the core is free and the alarm is up
            Run("extraction goto=cradle");
            Run("extraction transponder work");
            yield return Until(() => x.Lantern.Built && x.Lantern.Core != Entity.Null && EM.Exists(x.Lantern.Core) && EM.HasComponent<StationCore>(x.Lantern.Core), "the core spawns", 30);
            yield return new WaitForSeconds(2);
            Assert.AreEqual(6, EM.GetComponentData<StationCore>(x.Lantern.Core).Tethers, "six tethers hold the core");
            Run("extraction job accept=scrap");
            Run("extraction cut all");
            yield return Until(() => EM.GetComponentData<StationCore>(x.Lantern.Core).Freed != 0, "cutting every tether frees the core", 10);
            yield return Until(() => EM.GetComponentData<HeistState>(x.Lantern.Heist).Phase == HeistPhase.Alarm, "the alarm starts", 5);
            Assert.AreEqual(ExtractionRuntime.JobState.CoreTaken, x.State);
            Assert.AreEqual(6, Districts(EM, false), "every district is dark");

            // the magnet takes it
            StringAssert.StartsWith("Magnet on the core", Run("extraction magnet").Text);
            var player = Host.Context.PlayerShipRoot;
            Assert.IsTrue(TowApi.IsTowing(EM, player));
            yield return new WaitForSeconds(1);
            Assert.IsTrue(EM.Exists(player) && EM.GetComponentData<VoxelGrid>(player).Kind == GridKind.Ship, "the crew ship survives taking the core");

            // home: the core at the den's socket installs, the den's gun powers up and the job ends Installed
            var socket = EM.GetComponentData<LocalTransform>(x.Den.Socket).Position;
            var core = EM.GetComponentData<LocalTransform>(x.Lantern.Core); core.Position = socket; EM.SetComponentData(x.Lantern.Core, core);
            yield return Until(() => EM.GetComponentData<StationCore>(x.Lantern.Core).Socket == x.Den.Socket, "the core installs in the socket", 5);
            Assert.IsFalse(TowApi.IsTowing(EM, player), "the socket takes the core off the cable");
            Assert.IsTrue(CoreSocketSystem.DenHasPower(EM, DenPlace.DenId));
            yield return Until(() => x.State == ExtractionRuntime.JobState.Installed, "the job ends Installed", 3);
            using (var q = EM.CreateEntityQuery(ComponentType.ReadOnly<DenPowered>()))
            {
                using var guns = q.ToComponentDataArray<DenPowered>(Unity.Collections.Allocator.Temp);
                Assert.AreEqual(1, guns.Length);
                Assert.AreEqual(1, guns[0].On, "the den's gun has power");
            }

            // they come for it
            Run("extraction den attack");
            Assert.IsTrue(x.Den.Attacking);
            Assert.Greater(x.Den.Status(EM).IndexOf("wave=4", System.StringComparison.Ordinal), 0, x.Den.Status(EM));
        }

        static int Districts(EntityManager em, bool lit)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<District>());
            using var all = q.ToComponentDataArray<District>(Unity.Collections.Allocator.Temp);
            int n = 0; foreach (var d in all) if (d.Lit == lit) n++;
            return n;
        }
    }
}
