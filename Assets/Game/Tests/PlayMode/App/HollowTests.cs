using System.Collections;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Playground;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Mechanisms;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>DEV-023 option E, the Hollow, in the playground: it builds, its forts turn on their rocks, its guns are armed Clan
    /// turrets, the rock above the plane hides while the player is inside it, and its traffic flies on its own AI.</summary>
    public sealed class HollowTests : SessionCommandFixture
    {
        int Count<T>() where T : unmanaged, IComponentData
        { using var q = EM.CreateEntityQuery(ComponentType.ReadOnly<T>()); return q.CalculateEntityCount(); }

        [UnityTest] public IEnumerator Hollow_Builds_Turns_Arms_Cuts_Away_And_Flies()
        {
            Assume.That(HollowLayout.Pieces.Length, Is.GreaterThan(0), "the Hollow's layout is generated (ArtSource/Hub/hollow_codegen.py)");
            yield return RunCommand("playground");
            var go = Host.Commands.Execute("playground goto=hollow", Host.Context);
            Assert.AreEqual(DevResultKind.Ok, go.Kind, go.Text);
            var hollow = Host.Playground.Hollow;
            Assert.IsTrue(hollow.Built);
            Assert.AreEqual(HollowLayout.Pieces.Length + HollowLayout.Guns.Length + HollowLayout.Moorings.Length + HollowLayout.Boulders.Length
                + PlaygroundHollow.CoastWrecks.Length, hollow.GridCount, hollow.Missing);
            yield return Until(() => Count<CameraSeeThrough>() >= HollowLayout.Pieces.Length, "every piece of the rock spawns", 60);

            // the player arrives whole: no traffic spawns on the arrival point (two routes begin there)
            yield return new WaitForSeconds(6);
            var player = Host.Context.PlayerShipRoot;
            Assert.IsTrue(EM.Exists(player) && EM.GetComponentData<VoxelGrid>(player).Kind == GridKind.Ship, "the player survives arriving at the Hollow");
            // and no ship spawned on another: every traffic ship and gunship is still a flying, crewed ship
            using (var q = EM.CreateEntityQuery(ComponentType.ReadOnly<AiBrain>(), ComponentType.ReadOnly<VoxelGrid>()))
            {
                using var brains = q.ToEntityArray(Allocator.Temp);
                int flying = 0; foreach (var e in brains) if (EM.GetComponentData<VoxelGrid>(e).Kind == GridKind.Ship) flying++;
                Assert.GreaterOrEqual(flying, HollowLayout.Traffic.Length + HollowLayout.Gunships.Length, "no Hollow ship is wrecked on arrival");
            }

            // the forts turn steadily on their rocks
            using (var q = EM.CreateEntityQuery(ComponentType.ReadOnly<Mover>(), ComponentType.ReadOnly<CameraSeeThrough>(), ComponentType.ReadOnly<LocalTransform>()))
            {
                yield return Until(() => q.CalculateEntityCount() == HollowLayout.Forts.Length, $"the forts come up (movers {Count<Mover>()})", 30);
                using var forts = q.ToEntityArray(Allocator.Persistent);
                Assert.AreEqual(HollowLayout.Forts.Length, forts.Length, $"one turning grid per fort (movers {Count<Mover>()}, see-through {Count<CameraSeeThrough>()}, brains {Count<AiBrain>()})");
                if (forts.Length > 0)
                {
                    var fort = forts[0];
                    float rate = EM.GetComponentData<Mover>(fort).Rate;
                    Assert.Greater(math.abs(rate), 0, "the fort's rock spins");
                    var before = EM.GetComponentData<LocalTransform>(fort).Rotation;
                    double start = Time.timeAsDouble;
                    yield return new WaitForSeconds(1.5f);
                    var after = EM.GetComponentData<LocalTransform>(fort).Rotation;
                    float turned = math.degrees(2 * math.acos(math.min(1, math.abs(math.dot(before.value, after.value)))));
                    Assert.Greater(turned, .3f * math.degrees(math.abs(rate)) * (float)(Time.timeAsDouble - start), "the fort turned with its rock");
                    Assert.IsTrue(EM.HasComponent<AiBrain>(fort) && EM.HasComponent<Allegiance>(fort), "a fort's guns are an armed turret");
                }
            }

            // every gun is an armed Clan turret
            using (var q = EM.CreateEntityQuery(ComponentType.ReadOnly<AiBrain>(), ComponentType.ReadOnly<Allegiance>(), ComponentType.ReadOnly<VoxelGrid>()))
            {
                int turrets = 0;
                using var all = q.ToEntityArray(Allocator.Temp);
                foreach (var e in all)
                    if (EM.GetComponentData<VoxelGrid>(e).Kind == GridKind.Station && EM.GetComponentData<Allegiance>(e).Faction != 255) turrets++;
                Assert.GreaterOrEqual(turrets, HollowLayout.Guns.Length + HollowLayout.Forts.Length);
            }

            // inside the rock with the top-down camera, the rock above the flight plane is hidden; under a low camera the vault stays
            // (owner: "why would i want to see the stars if im inside an asteroid?"); outside, the whole rock shows
            int highPieces = 0;
            foreach (var p in HollowLayout.Pieces) if (p.Layer == HollowLayout.Layer.High) highPieces++;
            string inside = null;
            foreach (var st in HollowLayout.Stations) if (HollowLayout.Inside(st.Position)) { inside = st.Id; break; }
            Assume.That(inside, Is.Not.Null, "a station under the rock");
            string camera = global::Game.Presentation.Services.FlightCameraPreset.Current.Name;
            global::Game.Presentation.Services.FlightCameraPreset.TrySet("topdown");
            try
            {
                Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=" + inside, Host.Context).Kind);
                yield return Until(() => Count<GridRenderHidden>() == highPieces, "the top-down cutaway hides the rock above the plane inside", 5);
                global::Game.Presentation.Services.FlightCameraPreset.TrySet("chase");
                yield return Until(() => Count<GridRenderHidden>() == 0, "under the chase camera the vault stays", 5);
                global::Game.Presentation.Services.FlightCameraPreset.TrySet("topdown");
                Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=hollow", Host.Context).Kind);
                yield return Until(() => Count<GridRenderHidden>() == 0, "outside, the whole rock shows", 5);
            }
            finally { global::Game.Presentation.Services.FlightCameraPreset.TrySet(camera); }

            // traffic flies on its own AI along its routes
            using (var q = EM.CreateEntityQuery(ComponentType.ReadOnly<AiBrain>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<VoxelGrid>()))
            {
                yield return Until(() => q.CalculateEntityCount() >= HollowLayout.Traffic.Length, "the traffic spawns", 20);
                using var ships = q.ToEntityArray(Allocator.Persistent);
                var starts = new float3[ships.Length];
                for (int i = 0; i < ships.Length; i++) starts[i] = EM.GetComponentData<LocalTransform>(ships[i]).Position;
                yield return new WaitForSeconds(4);
                float moved = 0;
                for (int i = 0; i < ships.Length; i++)
                    if (EM.Exists(ships[i]) && EM.GetComponentData<VoxelGrid>(ships[i]).Kind == GridKind.Ship)
                        moved = math.max(moved, math.distance(starts[i], EM.GetComponentData<LocalTransform>(ships[i]).Position));
                Assert.Greater(moved, 20, "a ship flew its route");
            }
        }
    }
}
