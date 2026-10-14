using System;
using System.Collections;
using System.Linq;
using Game.Core;
using Game.Core.Events;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Tests.Common;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>GAL-012: exactly three production-boot cases, real content/loader/logic/lane owners and direct event drains.</summary>
    public sealed class GalaxyContentSmokeTests : GameFlowFixture
    {
        protected override bool QuietCampaign => false;   // exercises automatic story beats
        IEnumerator OpenWorld(ulong seed)
        {
            yield return NewGame(seed);
            var loader = Service<StarSystemLoader>(); var start = Service<GalaxyService>().Galaxy.StartStarSystem;
            for (int frame = 0; frame < 3 && (!loader.IsReady || !loader.Loaded.Equals(start)); frame++) yield return null;
            Assert.IsTrue(loader.IsReady, "The real SYS loader completes start entry within bounded frames."); Assert.AreEqual(start, loader.Loaded);
        }
        static T Definition<T>(ContentCatalog catalog, string id) where T : ContentDefinition
        { var value = catalog.Get<T>(new ContentId(id)); Assert.IsNotNull(value, id); return value; }
        static void ValidateWorld(ContentCatalog catalog)
        {
            var report = new ValidationReport("GAL-012 world definitions");
            var definitions = catalog.All<BodyTemplateDefinition>().Cast<ContentDefinition>()
                .Concat(catalog.All<BodyVisualDefinition>()).Concat(catalog.All<AuthoredStarSystemDefinition>())
                .Concat(catalog.All<StarSystemTemplateDefinition>()).Concat(catalog.All<GalaxyGenerationDefinition>());
            foreach (var definition in definitions)
                definition.Validate(report.For("content:" + definition.IdString), new ContentValidationContext("content:" + definition.IdString, false,
                    d => catalog.TryGet(d.Id, out var resolved) && ReferenceEquals(d, resolved), catalog));
            // These are the actual editor-owned cross-definition validators. Discover through their existing
            // interface rather than adding a forbidden Game.Editor reference to the PlayMode assembly.
            var validators = TypeCache.GetTypesDerivedFrom<IContentDatabaseValidator>();
            foreach (string name in new[] { "Game.Editor.Content.Validators.GalaxyContentValidator", "Game.Editor.Content.Validators.TravelContentValidator" })
            {
                var type = validators.SingleOrDefault(t => t.FullName == name); Assert.IsNotNull(type, name);
                ((IContentDatabaseValidator)Activator.CreateInstance(type)).Validate(catalog, report);
            }
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }
        static void CheckSystems(GalaxyService galaxy, ContentCatalog catalog)
        {
            var generation = galaxy.Definition;
            Assert.AreEqual("galaxy_main", generation.IdString); Assert.IsTrue(generation.UseFixedSeed); Assert.AreNotEqual(0UL, generation.FixedSeed);
            Assert.AreEqual(0, generation.ProceduralStarSystemCount); Assert.AreEqual(3, generation.AuthoredStarSystems.Count);
            Assert.AreEqual(2, generation.AuthoredLanes.Count); Assert.AreEqual("starsys_ark_start", generation.StartStarSystem.IdString);
            Assert.AreEqual("ark_station", generation.StartStarSystem.StartBodyKey);
            Assert.AreEqual("body_jump_point", generation.DefaultHyperlaneGate.IdString); Assert.AreEqual("body_gate_active", generation.DefaultAncientGate.IdString);
            Assert.AreEqual(3, galaxy.Galaxy.StarSystems.Length); Assert.AreEqual(2, galaxy.Galaxy.Lanes.Length);
            var rows = new[] { (Id: "starsys_ark_start", Name: "Ark Reach", X: 0f, Y: 0f, Danger: 0),
                (Id: "starsys_drift", Name: "The Drift", X: 6f, Y: 2f, Danger: 2), (Id: "starsys_whisper", Name: "Whisper", X: -5f, Y: 4f, Danger: 1) };
            foreach (var row in rows)
            {
                var definition = Definition<AuthoredStarSystemDefinition>(catalog, row.Id);
                Assert.Contains(definition, generation.AuthoredStarSystems.ToArray()); Assert.IsFalse(definition.ConnectProcedurally, "No unintended third lane.");
                Assert.AreEqual(new Vector2(row.X, row.Y), definition.PositionLy); Assert.AreEqual(row.Danger, definition.Danger);
                Assert.IsTrue(galaxy.TryResolveStarSystem(definition.Id, out var id)); var generated = galaxy.Galaxy.Get(id);
                Assert.AreEqual(row.Name, galaxy.GetStarSystemName(id)); Assert.AreEqual(new double2(row.X, row.Y), generated.PositionLy);
                Assert.AreEqual(row.Danger, generated.Danger); Assert.AreNotEqual(StarSystemFlags.None, generated.Flags & StarSystemFlags.Authored);
                var content = galaxy.GetContent(id); Assert.AreSame(content, galaxy.GetContent(id), "Use the real owner's content cache.");
                Assert.AreEqual(1 + definition.Bodies.Count + generated.LaneCount, content.Bodies.Length);
                Assert.AreEqual("body_star", galaxy.GetBodyTemplate(content.Bodies[0].TemplateIndex).IdString);
            }
            var ark = Definition<AuthoredStarSystemDefinition>(catalog, "starsys_ark_start");
            var drift = Definition<AuthoredStarSystemDefinition>(catalog, "starsys_drift");
            var whisper = Definition<AuthoredStarSystemDefinition>(catalog, "starsys_whisper");
            Assert.IsTrue(generation.AuthoredLanes.Any(l => l.a == ark && l.b == drift || l.a == drift && l.b == ark));
            var gate = generation.AuthoredLanes.Single(l => l.a == ark && l.b == whisper || l.a == whisper && l.b == ark);
            Assert.AreEqual("$gate_ark_start_active", gate.condition.Source); Assert.IsFalse(gate.condition.IsStale); Assert.IsFalse(gate.condition.HasErrors);
            Assert.AreEqual("The gate is dormant.", gate.lockedMessage); Assert.AreEqual("body_gate_dormant", gate.gateTemplateA.IdString);
            Assert.AreEqual("body_gate_dormant", gate.gateTemplateB.IdString);
            Assert.IsTrue(Definition<FactionDefinition>(catalog, "faction_ark").OwnedStarSystems.Contains(ark));
            Assert.IsTrue(Definition<FactionDefinition>(catalog, "faction_vestari").OwnedStarSystems.Contains(whisper));
            Assert.IsFalse(catalog.All<FactionDefinition>().Any(f => f.OwnedStarSystems.Contains(drift)));
        }
        [UnityTest] public IEnumerator MainGalaxy_GeneratesAndValidates()
        {
            yield return OpenWorld(0xACCE5512UL); // Deliberately differs from the authored fixed seed.
            var galaxy = Service<GalaxyService>(); var catalog = Service<ContentService>().Catalog;
            ValidateWorld(catalog); CheckSystems(galaxy, catalog);
            Assert.AreEqual(1, galaxy.GenerationCount); Assert.AreEqual(galaxy.Definition.FixedSeed, galaxy.State.Seed);
            var start = galaxy.Galaxy.StartStarSystem; Assert.AreEqual(start, galaxy.State.CurrentStarSystem);
            Assert.IsTrue(galaxy.State.IsKnown(start)); Assert.IsTrue(galaxy.State.IsVisited(start));
            var loader = Service<StarSystemLoader>();
            Assert.IsTrue(loader.IsReady); Assert.AreEqual(start, loader.Loaded); Assert.AreNotEqual(Entity.Null, PlayerRoot);
            using var info = Em.CreateEntityQuery(ComponentType.ReadOnly<CurrentStarSystemInfo>());
            Assert.AreEqual(1, info.CalculateEntityCount()); var current = info.GetSingleton<CurrentStarSystemInfo>();
            Assert.AreEqual(start, current.Id); Assert.AreEqual(1, current.Loaded); Assert.AreEqual(0, current.Changing);
            using var anchors = Em.CreateEntityQuery(ComponentType.ReadOnly<StarSystemBody>(), ComponentType.ReadOnly<StarSystemMember>());
            anchors.SetSharedComponentFilter(new StarSystemMember { Id = start });
            Assert.AreEqual(galaxy.GetContent(start).Bodies.Length, anchors.CalculateEntityCount()); Assert.AreEqual(anchors.CalculateEntityCount(), current.BodyCount);
            using var bodies = anchors.ToComponentDataArray<StarSystemBody>(Allocator.Temp);
            foreach (var body in bodies) Assert.AreEqual(start, body.Id.System);
        }
        static BodySpec Body(GalaxyService galaxy, string system, string key, string template, BodyRole role)
        {
            Assert.IsTrue(galaxy.TryResolveBody(new ContentId(system), key, out var id), system + "/" + key); Assert.IsTrue(id.IsValid);
            var content = galaxy.GetContent(id.System); Assert.IsTrue(content.TryFind(id.LocalKey, out int index)); var body = content.Bodies[index];
            Assert.AreEqual(role, body.Id.Role); Assert.AreEqual(template, galaxy.GetBodyTemplate(body.TemplateIndex).IdString);
            Assert.AreEqual(system + "/" + key, galaxy.GetLocationKey(id)); return body;
        }
        [UnityTest] public IEnumerator OpeningChapter_BodiesResolve()
        {
            yield return OpenWorld(0x5EED0001UL); var galaxy = Service<GalaxyService>(); var catalog = Service<ContentService>().Catalog;
            var rows = new[] {
                ("starsys_ark_start", "ark_station", "body_station_ark", BodyRole.Station),
                ("starsys_ark_start", "reach_i", "body_planet_rocky", BodyRole.Planet),
                ("starsys_ark_start", "reach_ii", "body_planet_terrestrial", BodyRole.Planet),
                ("starsys_ark_start", "reach_ii_a", "body_moon", BodyRole.Moon),
                ("starsys_ark_start", "reach_belt", "body_asteroid_belt", BodyRole.Belt),
                ("starsys_drift", "drift_belt", "body_asteroid_belt", BodyRole.Belt),
                ("starsys_drift", "drift_derelict", "body_derelict_ship", BodyRole.PointOfInterest),
                ("starsys_drift", "drift_cache", "body_resource_cache", BodyRole.PointOfInterest),
                ("starsys_drift", "drift_lab", "body_station_abandoned", BodyRole.Station),
                ("starsys_whisper", "whisper_i", "body_planet_ice_giant", BodyRole.Planet),
                ("starsys_whisper", "whisper_ii", "body_planet_barren", BodyRole.Planet),
                ("starsys_whisper", "whisper_outpost", "body_station", BodyRole.Station) };
            foreach (var row in rows) Body(galaxy, row.Item1, row.Item2, row.Item3, row.Item4);
            Assert.AreEqual(5, Definition<AuthoredStarSystemDefinition>(catalog, "starsys_ark_start").Bodies.Count);
            Assert.AreEqual(4, Definition<AuthoredStarSystemDefinition>(catalog, "starsys_drift").Bodies.Count);
            Assert.AreEqual(3, Definition<AuthoredStarSystemDefinition>(catalog, "starsys_whisper").Bodies.Count);
            var tags = new[] { ("body_station_ark", "ark_station"), ("body_station", "station"), ("body_station_abandoned", "abandoned_station"),
                ("body_derelict_ship", "derelict"), ("body_debris_field", "debris_field"), ("body_resource_cache", "resource_cache"),
                ("body_gate_dormant", "gate_dormant"), ("body_gate_active", "gate_active"), ("body_jump_point", "jump_point"),
                ("body_asteroid_belt", "asteroid_belt"), ("body_planet_barren", "barren_planet"), ("body_planet_gas_giant", "gas_giant"),
                ("body_planet_ice", "ice_planet"), ("body_planet_ice_giant", "ice_giant"), ("body_planet_rocky", "rocky_planet"),
                ("body_planet_terrestrial", "terrestrial_planet"), ("body_planet_water", "ocean_planet"), ("body_moon", "moon"), ("body_star", "star"), ("body_star_blackhole", "black_hole") };
            foreach (var row in tags) Assert.AreEqual(row.Item2, Definition<BodyTemplateDefinition>(catalog, row.Item1).StoryTag, row.Item1);
            var moon = Body(galaxy, "starsys_ark_start", "reach_ii_a", "body_moon", BodyRole.Moon);
            var planet = Body(galaxy, "starsys_ark_start", "reach_ii", "body_planet_terrestrial", BodyRole.Planet);
            Assert.AreEqual(planet.Id.LocalKey, moon.ParentKey); Assert.AreEqual(planet.Orbit, moon.ParentOrbit);
            Assert.AreEqual("Ark Reach II a", galaxy.GetBodyName(moon.Id));
            var belt = Body(galaxy, "starsys_drift", "drift_belt", "body_asteroid_belt", BodyRole.Belt);
            var cache = Body(galaxy, "starsys_drift", "drift_cache", "body_resource_cache", BodyRole.PointOfInterest);
            Assert.Less(cache.Orbit.RadiusM, belt.BeltOuterRadiusM); Assert.Greater(cache.Orbit.RadiusM, belt.BeltInnerRadiusM);
            Assert.AreEqual(1000, Definition<BodyTemplateDefinition>(catalog, "body_asteroid_belt").SectorSizeM);
            foreach (string station in new[] { "body_station_ark", "body_station" }) Assert.AreEqual(InteractionKind.Dock, Definition<BodyTemplateDefinition>(catalog, station).Interaction);
            var outpost = Definition<BodyTemplateDefinition>(catalog, "body_station"); Assert.AreEqual(StructureSource.Blueprint, outpost.StructureSource);
            Assert.AreEqual("ship_vestari", outpost.Blueprint.IdString);
            Assert.IsTrue(VoxelStructureCodec.TryDecode(outpost.Blueprint.Payload, catalog, out var structure, out string error), error);
            using (structure)
            using (var blocks = new NativeList<StructureBlock>(Allocator.TempJob))
            {
                structure.GetBlocks(blocks); bool turret = false;
                foreach (var placed in blocks)
                {
                    var block = catalog.GetBlock(placed.Placement.Block); Assert.AreNotSame(catalog.MissingBlock, block);
                    if (block.TryGetModule<WeaponMountModule>(out var mount) && mount.Weapon != null && mount.Weapon.TraverseHalfArcDeg > 0) turret = true;
                }
                Assert.IsTrue(turret, "The actual outpost blueprint includes a working turret weapon, not a metadata-only reference.");
            }
            Assert.IsFalse(galaxy.TryResolveBody(new ContentId("starsys_drift"), "not_a_body", out _));
        }
        static void SetGateFact(ContentCatalog catalog, ILogicContext context, bool open)
        {
            var refs = RefResolverRegistry.CreateDefault(catalog); var report = new ValidationReport("Whisper gate fact");
            var compiler = new LogicCompiler(context.Facts.Catalog, refs, new LogicFunctionRegistry());
            Assert.IsTrue(compiler.TryCompileCommand("set_fact gate_ark_start_active " + (open ? "true" : "false"),
                new LogicCompileOptions(LogicUsage.DialogCommand, "test:GAL-012", "gate"), report, out var effect), report.ToText());
            effect.Bind(new LogicBinder(context.Facts.Catalog, refs, report)); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.IsTrue(effect.CanApply(context)); effect.Apply(context); EventHub.DrainAll();
        }
        [UnityTest] public IEnumerator WhisperLane_GatedByFact()
        {
            yield return OpenWorld(0x5EED0001UL); var galaxy = Service<GalaxyService>(); var catalog = Service<ContentService>().Catalog;
            var access = Service<ILaneAccess>(); var travel = Service<TravelService>(); var context = Service<ILogicContext>(); Assert.AreSame(travel, access);
            Assert.IsTrue(galaxy.TryResolveStarSystem(new ContentId("starsys_ark_start"), out var ark));
            Assert.IsTrue(galaxy.TryResolveStarSystem(new ContentId("starsys_whisper"), out var whisper));
            Assert.IsTrue(galaxy.TryResolveStarSystem(new ContentId("starsys_drift"), out var drift));
            Assert.IsTrue(galaxy.Galaxy.TryFindLane(ark, whisper, out int gated)); Assert.IsTrue(galaxy.Galaxy.TryFindLane(ark, drift, out int open));
            Assert.IsFalse(access.IsLaneOpen(gated)); Assert.AreEqual("The gate is dormant.", access.GetLockedMessage(gated)); Assert.IsTrue(access.IsLaneOpen(open));
            Assert.IsFalse(travel.CanSetDestination(whisper, out string reason)); Assert.AreEqual("The gate is dormant.", reason);
            var gate = galaxy.Galaxy.Lanes[gated]; Assert.Greater(gate.GateIndex, 0); Assert.AreEqual(0, galaxy.Galaxy.Lanes[open].GateIndex);
            Assert.IsFalse(galaxy.Galaxy.LaneGates[gate.GateIndex - 1].Condition.Evaluate(context)); uint revision = access.Revision;
            SetGateFact(catalog, context, true);
            Assert.IsTrue(access.IsLaneOpen(gated)); Assert.IsTrue(access.IsLaneOpen(open)); Assert.AreEqual("", access.GetLockedMessage(gated)); Assert.Greater(access.Revision, revision);
            Assert.IsTrue(galaxy.Galaxy.LaneGates[gate.GateIndex - 1].Condition.Evaluate(context)); Assert.IsTrue(travel.CanSetDestination(whisper, out _));
            uint changed = access.Revision; SetGateFact(catalog, context, true); Assert.AreEqual(changed, access.Revision, "No flip for the same fact value.");
            foreach (var system in new[] { ark, whisper })
            {
                bool found = false;
                foreach (var body in galaxy.GetContent(system).Bodies)
                    if (body.Id.Role == BodyRole.LaneGate && body.LaneIndex == gated)
                    { Assert.AreEqual("body_gate_dormant", galaxy.GetBodyTemplate(body.TemplateIndex).IdString); Assert.AreEqual(OrbitMode.Fixed, body.Orbit.Mode); found = true; }
                Assert.IsTrue(found, "Real gated lane creates its dormant gate at both ends.");
            }
            SetGateFact(catalog, context, false); Assert.IsFalse(access.IsLaneOpen(gated)); Assert.IsTrue(access.IsLaneOpen(open));
            Assert.AreEqual("The gate is dormant.", access.GetLockedMessage(gated));
        }
    }
}
