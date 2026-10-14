using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Travel;
using Game.Presentation.Components;
using Game.Presentation.Presenters;
using Game.Presentation.Systems;
using Game.Simulation.Factions;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Galaxy.Travel;
using Game.UI.ViewModels;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Presentation
{
    public sealed class HudSyncSystemTests
    {
        sealed class Loader : ITravelLoader
        {
            public StarSystemId Loaded { get; set; }
            public bool IsReady => true;
            public bool IsArrivalReady => true;
            public bool IsAreaReady(double3 position) => true;
            public bool BeginStarSystemChange(StarSystemId target, double3 position, float yaw, StarSystemEntryReason reason)
                => throw new NotSupportedException("HUD queries do not initiate travel.");
        }
        sealed class Fixture : IDisposable
        {
            internal readonly LaneAccessTests.Fixture Owners;
            internal readonly Loader Loader = new();
            internal readonly HudViewModel Model = new();
            internal readonly HudSyncSystem Sync;
            internal readonly HudNames Names;
            internal readonly NativeArray<HudRadarBlip> Radar = new(0, Allocator.Persistent);
            internal HudSnapshot Snapshot = new() { Flags = HudFlags.HasShip | HudFlags.HasTarget, Hull01 = .5f, TargetNameHash = new ContentId("ship_hud_a").Hash, TargetFaction = 0 };
            internal Fixture(bool chain = false)
            {
                Owners = new LaneAccessTests.Fixture(loader: Loader, authoredCount: 3, generate: false);
                if (chain) foreach (var definition in Owners.Content.Definitions)
                    if (definition is AuthoredStarSystemDefinition)
                    { using var serialized = new UnityEditor.SerializedObject(definition); TestContentFactory.Set(serialized, "connectProcedurally", false); serialized.ApplyModifiedPropertiesWithoutUndo(); }
                Owners.Galaxy.NewGame(42);
                Loader.Loaded = Owners.Galaxy.Galaxy.StartStarSystem; Owners.State.CurrentStarSystem = Loader.Loaded;
                foreach (var system in Owners.Galaxy.Galaxy.StarSystems) Owners.Galaxy.Reveal(system.Id);
                var definitions = new List<ContentDefinition>(Owners.Content.Definitions);
                definitions.Add(TestContentFactory.Create<FactionDefinition>("faction_hud", so => { TestContentFactory.Set(so, "isPlayerFaction", true); TestContentFactory.Set(so, "shipNamePrefix", "HUD"); }));
                definitions.Add(TestContentFactory.Create<ShipBlueprintDefinition>("ship_hud_a", so => TestContentFactory.Set(so, "displayName", "Alpha")));
                definitions.Add(TestContentFactory.Create<ShipBlueprintDefinition>("ship_hud_b", so => TestContentFactory.Set(so, "displayName", "Beta")));
                var catalog = ContentCatalog.Build(definitions, new ValidationReport("HUD names"));
                Names = new HudNames(catalog, FactionIndexMap.Build(catalog), Owners.Galaxy, Owners.Travel);
                Sync = Owners.World.GetOrCreateSystemManaged<HudSyncSystem>(); Sync.Bind(Model, Names, _ => "F");
            }
            internal void Apply() => Sync.Apply(Snapshot, Radar, default, false, 0);
            public void Dispose() { Radar.Dispose(); Owners.Dispose(); }
        }
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); }
        [Test] public void Unchanged_NoNotifications()
        {
            using var f = new Fixture(); f.Apply(); int changed = 0; f.Model.propertyChanged += (_, _) => changed++;
            f.Apply(); Assert.Zero(changed);
        }
        [Test] public void Quantisation_SmallChangeIgnored()
        {
            using var f = new Fixture(); f.Apply(); int changed = 0; f.Model.propertyChanged += (_, _) => changed++;
            f.Snapshot.Hull01 += .0001f; f.Snapshot.SpeedMps = .1f; f.Snapshot.TargetDistanceM = .1f; f.Apply(); Assert.Zero(changed);
        }
        [Test] public void TargetNameChange_SingleStringAssign()
        {
            using var f = new Fixture(); f.Apply(); int changed = 0;
            f.Model.propertyChanged += (_, args) => { if (args.propertyName == nameof(HudViewModel.TargetName)) changed++; };
            f.Snapshot.TargetNameHash = new ContentId("ship_hud_b").Hash; f.Apply(); f.Apply();
            Assert.AreEqual("HUD Beta", f.Model.TargetName); Assert.AreEqual(1, changed);
        }
        [Test] public void GatePrompt_FromGateInfo()
        {
            using var f = new Fixture(); var content = f.Owners.Galaxy.GetContent(f.Loader.Loaded);
            foreach (var body in content.Bodies) if (body.Id.Role == BodyRole.LaneGate) { f.Snapshot.InteractionBodyKey = body.Id.LocalKey; break; }
            Assert.AreNotEqual(0ul, f.Snapshot.InteractionBodyKey);
            f.Snapshot.Flags |= HudFlags.HasInteraction; f.Snapshot.InteractionKind = (byte)InteractionKind.LaneGate;
            f.Snapshot.StarSystemIdValue = f.Loader.Loaded.Value; f.Apply();
            Assert.AreEqual("Gate is asleep.", f.Model.InteractionDetailText); Assert.IsTrue(f.Model.InteractionBlocked);
            f.Owners.Set(true); f.Owners.Logic.Reads.Items[f.Owners.Catalog.IndexOf(f.Owners.Travel.Config.FuelItem)] = 20;
            Assert.IsTrue(f.Owners.Travel.TryGetGateInfo(f.Snapshot.InteractionBodyKey, out var gate)); f.Apply();
            Assert.AreEqual(gate.TargetName, f.Model.InteractionTargetName); Assert.AreEqual("Fuel " + gate.FuelCost, f.Model.InteractionDetailText); Assert.IsFalse(f.Model.InteractionBlocked);
            f.Owners.Logic.Reads.Items[f.Owners.Catalog.IndexOf(f.Owners.Travel.Config.FuelItem)] = 0; f.Apply(); Assert.IsTrue(f.Model.InteractionBlocked, "Fuel changes invalidate the cached prompt.");
        }
        [Test] public void RouteText_Formatted()
        {
            using var f = new Fixture(chain: true); f.Owners.Set(true);
            var galaxy = f.Owners.Galaxy.Galaxy;
            for (int i = 0; i < galaxy.Lanes.Length; i++) { var lane = galaxy.Lanes[i]; lane.LengthLy = 2 * (i + 1); galaxy.Lanes[i] = lane; }
            var destination = StarSystemId.FromAuthored(new ContentId("starsys_fixture_2").Hash);
            Assert.IsTrue(f.Owners.Travel.SetDestination(destination, out var reason), reason); f.Apply();
            Assert.AreEqual(2, f.Owners.Travel.Route.Count); Assert.AreEqual(3, f.Owners.Travel.RouteFuelCost);
            Assert.AreEqual("Route: 2 jumps, 3 fuel", f.Model.RouteText); Assert.IsTrue(f.Model.HasRoute);
            f.Owners.Travel.ClearDestination(); f.Apply(); Assert.IsFalse(f.Model.HasRoute);
        }
        static void Change(ref HudSnapshot value, int i)
        {
            float fraction = (i % 500) / 500f;
            value.Hull01 = fraction; value.SpeedMps = i; value.TargetDistanceM = i;
            value.ShieldHp = i; value.ShieldMax = i + 1; value.ShieldStatus = (byte)(i % 4);
            value.Energy = i; value.EnergyMax = i + 2; value.TargetHull01 = fraction; value.TargetShield01 = 1 - fraction;
            value.TargetRelation = (byte)(i % 3); value.Flags = HudFlags.HasShip | HudFlags.HasTarget | (i % 2 == 0 ? HudFlags.Boosting : 0);
            value.WeaponGroups.Clear(); value.Abilities.Clear();
            for (byte n = 0; n < 3; n++) value.WeaponGroups.Add(new HudWeaponGroupState { Group = n, WeaponCount = (byte)(i % 255), Ready01 = fraction, Firing = (byte)(i % 2) });
            for (int n = 0; n < 4; n++) value.Abilities.Add(new HudAbilityState { Cooldown01 = fraction, Charges = (byte)(i % 255), Ready = (byte)(i % 2), Active = (byte)(i % 2) });
        }
        [Test] public void AllChanging_ZeroAlloc()
        {
            using var f = new Fixture(); f.Apply();
            for (int i = 0; i < 20; i++) { Change(ref f.Snapshot, i); f.Apply(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) { Change(ref f.Snapshot, i); f.Apply(); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(599, f.Model.SpeedMps); Assert.AreEqual(599, f.Model.TargetDistanceM);
            if (bytes != 0) GameLog.Warn("HUD", "HudSyncSystem 600 changed snapshots allocated " + bytes + " bytes; target 0 (ADR-0042).");
        }
    }
}
