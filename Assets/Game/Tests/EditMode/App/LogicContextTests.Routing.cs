using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.App.Services;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation.Commands;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Galaxy;
using Game.Voxel.Data;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.App
{
    public sealed partial class LogicContextTests
    {
        [Test] public unsafe void WorldAdapter_RoutesOrLogsOnce()
        {
            using var fixture = new TestGalaxyFixtures.Fixture(2, 0, 42, false);
            using var session = new SessionFixture();
            var ship = TestContentFactory.Create<ShipBlueprintDefinition>("ship_narrative_test");
            var cue = TestContentFactory.Create<AudioCueDefinition>("cue_narrative_test");
            var config = TestContentFactory.Create<NarrativeConfigDefinition>("narrative_test");
            fixture.Definitions.Add(ship); fixture.Definitions.Add(cue);
            var catalog = ContentCatalog.Build(fixture.Definitions, new ValidationReport("routing"));
            using (var source = new VoxelStructure())
            {
                ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = catalog.GetBlockIndex(fixture.Hull) });
                source.SetCell(int3.zero, Cell.Palette(palette));
                ship.EditorSetPayload(VoxelStructureCodec.Encode(source, VoxsBlockNames.FromCatalog(catalog), VoxsEncodeOptions.ForAsset),
                    1, Vector3Int.zero, Vector3Int.zero);
            }
            var world = session.World.World; var em = world.EntityManager;
            using var galaxy = new GalaxyService(catalog, session.Session.Galaxy, world, new GalaxyFrameTicker());
            galaxy.NewGame(42);
            using var blueprints = new BlueprintLibrary(catalog, default);
            var audio = new RecordingAudio();
            using var adapter = new LogicWorldAdapter(catalog, session.Session, galaxy, blueprints, audio, null, world, config);
            var clock = em.CreateEntity(typeof(SimClock)); em.SetComponentData(clock, new SimClock { UniversalTime = 19 });
            Assert.AreEqual(19, adapter.UniversalTime);
            Assert.AreEqual(catalog.IndexOf(galaxy.Definition.StartStarSystem), adapter.CurrentStarSystemIndex);
            adapter.GiveShip(catalog.IndexOf(ship)); Assert.AreEqual(1, session.Session.Fleet.Ships.Count);
            Assert.AreEqual(1, session.Session.Fleet.Flagship.Structure.SolidCellCount);
            adapter.PlaySfx(catalog.IndexOf(cue)); Assert.AreEqual(1, audio.Calls); Assert.AreEqual(cue.Id, audio.Last);
            adapter.Notify("Narrative route fixture");
            if (GameLog.GetOnceCount("Narrative.Editor.Rejected") == 0)
                LogAssert.Expect(LogType.Warning, new Regex("OpenShipEditor was rejected"));
            adapter.OpenShipEditor(); // Real flow rejects MainMenu fleet entry; it must be observed, not thrown away.
            var content = galaxy.GetContent(galaxy.State.CurrentStarSystem); var body = content.Bodies[0];
            var scope = new LogicScope(LogicUsage.BeatEffect, 0, 0, 1, true,
                new LogicBodyHandle(body.Id.System.Value, (byte)body.Id.Role, body.Id.Slot, body.Id.SubSlot, default));
            var sink = new RecordingSink(); galaxy.SetLiveDeltaSink(sink);
            adapter.ReplaceBody(scope, catalog.IndexOf(fixture.Planet)); Assert.AreEqual(1, sink.Changes);
            var delta = galaxy.State.GetOrCreateDelta(body.Id.System);
            delta.SetStructure(body.Id.LocalKey, 1, new VoxelStructure());
            adapter.RemoveBody(scope); Assert.AreEqual(1, sink.Removals); Assert.AreEqual(2, sink.Changes);
            Assert.IsFalse(delta.TryGetStructure(body.Id.LocalKey, out _)); Assert.IsTrue(galaxy.IsBodyDestroyed(body.Id));
            galaxy.OnSessionRestored(); Assert.IsTrue(galaxy.IsBodyDestroyed(body.Id));
            Assert.IsFalse(delta.TryGetStructure(body.Id.LocalKey, out _));
            world.CreateSystem<DamageStreamsSystem>();
            var player = em.CreateEntity(typeof(PlayerShip), typeof(VoxelGrid));
            em.SetComponentData(player, new VoxelGrid { BrickDims = new int3(1), CellSize = .5f, CellMin = int3.zero, CellMax = int3.zero });
            var brick = em.CreateEntity(); var masks = em.AddBuffer<BrickSolidMask>(brick); masks.ResizeUninitialized(64);
            for (int i = 0; i < masks.Length; i++) masks[i] = default;
            masks[0] = new BrickSolidMask { Bits = 1 };
            em.AddBuffer<BrickSlot>(player).Add(new BrickSlot { Brick = brick });
            adapter.RepairPlayerShip(25); adapter.DamagePlayerShip(2, scope);
            using var query = em.CreateEntityQuery(typeof(DamageStreams)); var streams = query.GetSingleton<DamageStreams>();
            Assert.AreEqual(1, streams.Restores.Data->Inbox.Length); Assert.AreEqual(.25f, streams.Restores.Data->Inbox[0].HpFraction);
            Assert.AreEqual(1, streams.Damage.Data->Inbox.Length); var hit = streams.Damage.Data->Inbox[0];
            Assert.AreEqual(player, hit.Grid); Assert.AreEqual(new float3(.25f), hit.LocalPoint);
            Assert.AreEqual(2 * config.HullDamagePerPoint, hit.Amount); Assert.AreEqual(.75f, hit.Radius);
            bool closed = false;
            var calls = new Dictionary<string, Action>
            {
                [nameof(adapter.GetRelation)] = () => adapter.GetRelation(1),
                [nameof(adapter.AdjustRelation)] = () => adapter.AdjustRelation(1, 1),
                [nameof(adapter.CurrentStarSystemOwnerFactionIndex)] = () => { _ = adapter.CurrentStarSystemOwnerFactionIndex; },
                [nameof(adapter.SpawnEncounter)] = () => adapter.SpawnEncounter(1, "", 1, scope),
                [nameof(adapter.StartCombat)] = () => adapter.StartCombat(1, scope),
                [nameof(adapter.RequestLaneJump)] = () => adapter.RequestLaneJump(1),
                [nameof(adapter.OpenMerchant)] = () => adapter.OpenMerchant(1, scope, () => closed = true)
            };
            foreach (string name in LogicWorldAdapter.UnwiredMembers)
            {
                string key = "Narrative.Unwired." + name; int before = GameLog.GetOnceCount(key);
                if (before == 0) LogAssert.Expect(LogType.Error, new Regex(name + " not wired:"));
                calls[name](); calls[name](); Assert.AreEqual(before + 2, GameLog.GetOnceCount(key));
            }
            Assert.IsTrue(closed);
        }
        sealed class RecordingSink : IGalaxyLiveDeltaSink
        {
            internal int Changes, Removals;
            public void PrepareBodyRemoval(BodyId body) => Removals++;
            public void OnBodyDeltaChanged(BodyId body) => Changes++;
            public void FlushStructures() { }
        }
        sealed class RecordingAudio : IAudioService
        {
            internal int Calls; internal ContentId Last;
            public void PlayCue(ContentId cue) { Calls++; Last = cue; }
            public void PlayUi(UiSound sound) { }
            public void PlayCueAt(ContentId cue, float3 position, float volume) { }
            public void PlayStinger(ContentId cue) { }
            public void SetMusicOverride(ContentId cue, float fadeSeconds) { }
            public void ClearMusicOverride(float fadeSeconds) { }
        }
    }
}
