using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Galaxy.Encounters;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    public sealed class ComposerTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();

        static ShipBlueprintDefinition Blueprint(string id, float cellSize = .5f)
            => TestContentFactory.Create<ShipBlueprintDefinition>(id, so =>
            {
                TestContentFactory.Set(so, "cellSize", cellSize);
                so.FindProperty("boundsMin").vector3IntValue = new Vector3Int(-4, -2, -3);
                so.FindProperty("boundsMax").vector3IntValue = new Vector3Int(1, 3, 5);
            });

        static EncounterDefinition Explicit(FactionDefinition faction, params EncounterShipEntry[] entries)
            => TestContentFactory.Create<EncounterDefinition>("encounter_test", so =>
            {
                TestContentFactory.Set(so, "faction", faction);
                var rows = so.FindProperty("ships"); rows.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var row = rows.GetArrayElementAtIndex(i); var entry = entries[i];
                    row.FindPropertyRelative("blueprint").objectReferenceValue = entry.blueprint;
                    row.FindPropertyRelative("aiProfile").objectReferenceValue = entry.aiProfile;
                    row.FindPropertyRelative("countMin").intValue = entry.countMin;
                    row.FindPropertyRelative("countMax").intValue = entry.countMax;
                }
            });

        [Test] public void Deterministic()
        {
            var fallback = TestContentFactory.Create<AiProfileDefinition>("ai_fallback");
            var own = TestContentFactory.Create<AiProfileDefinition>("ai_own");
            var faction = TestContentFactory.Create<FactionDefinition>("faction_test", so => TestContentFactory.Set(so, "defaultAiProfile", fallback));
            ShipBlueprintDefinition a = Blueprint("ship_a"), b = Blueprint("ship_b", 2f);
            var def = Explicit(faction,
                new EncounterShipEntry { blueprint = a, countMin = 2, countMax = 4 },
                new EncounterShipEntry { blueprint = b, aiProfile = own, countMin = 1, countMax = 3 });
            var first = new List<PlannedShip> { default }; var second = new List<PlannedShip>();
            EncounterComposer.Compose(def, 42, 100f, first);
            EncounterComposer.Compose(def, 42, .01f, second);
            CollectionAssert.AreEqual(first, second);
            var rng = RngStreams.For(42, RngPurpose.EncounterFleet, 0);
            int aCount = rng.NextInt(2, 5), bCount = rng.NextInt(1, 4);
            Assert.That(first.Count, Is.EqualTo(aCount + bCount));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(first[i].Blueprint, Is.SameAs(i < aCount ? a : b));
                Assert.That(first[i].Profile, Is.SameAs(i < aCount ? fallback : own));
                // |max(abs(min), abs(max + 1))| = |(4, 4, 6)| = sqrt(68).
                Assert.That(first[i].RadiusM, Is.EqualTo((i < aCount ? .5f : 2f) * Mathf.Sqrt(68f)).Within(1e-5));
            }
        }

        [Test] public void DrawsForMaximum()
        {
            var faction = TestContentFactory.Create<FactionDefinition>("faction_test");
            ShipBlueprintDefinition a = Blueprint("ship_a"), b = Blueprint("ship_b");
            var def = Explicit(faction,
                new EncounterShipEntry { blueprint = a, countMin = 1, countMax = 2 },
                new EncounterShipEntry { blueprint = b, countMin = 1, countMax = 4 });
            var before = new List<PlannedShip>(); var after = new List<PlannedShip>();
            for (ulong seed = 1; seed <= 8; seed++)
            {
                TestContentFactory.Edit(def, so => so.FindProperty("ships").GetArrayElementAtIndex(0).FindPropertyRelative("countMax").intValue = 2);
                EncounterComposer.Compose(def, seed, .5f, before);
                TestContentFactory.Edit(def, so => so.FindProperty("ships").GetArrayElementAtIndex(0).FindPropertyRelative("countMax").intValue = 6);
                EncounterComposer.Compose(def, seed, .5f, after);
                var rng = RngStreams.For(seed, RngPurpose.EncounterFleet, 0);
                rng.NextInt(1, 3); int expectedLater = rng.NextInt(1, 5);
                Assert.That(before.FindAll(s => s.Blueprint == b).Count, Is.EqualTo(expectedLater));
                Assert.That(after.FindAll(s => s.Blueprint == b).Count, Is.EqualTo(expectedLater));
            }
        }

        [Test] public void Doctrine_Weighted()
        {
            ShipBlueprintDefinition a = Blueprint("ship_a"), b = Blueprint("ship_b");
            var fallback = TestContentFactory.Create<AiProfileDefinition>("ai_fallback");
            var own = TestContentFactory.Create<AiProfileDefinition>("ai_own");
            var faction = TestContentFactory.Create<FactionDefinition>("faction_test", so =>
            {
                TestContentFactory.Set(so, "defaultAiProfile", fallback);
                var rows = so.FindProperty("doctrine"); rows.arraySize = 2;
                rows.GetArrayElementAtIndex(0).FindPropertyRelative("blueprint").objectReferenceValue = a;
                rows.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1;
                rows.GetArrayElementAtIndex(1).FindPropertyRelative("blueprint").objectReferenceValue = b;
                rows.GetArrayElementAtIndex(1).FindPropertyRelative("weight").floatValue = 3;
                rows.GetArrayElementAtIndex(1).FindPropertyRelative("aiProfile").objectReferenceValue = own;
            });
            var def = TestContentFactory.Create<EncounterDefinition>("encounter_test", so =>
            {
                TestContentFactory.Set(so, "faction", faction); TestContentFactory.Set(so, "useFactionDoctrine", true);
                TestContentFactory.Set(so, "doctrineMinShips", 2); TestContentFactory.Set(so, "doctrineMaxShips", 5);
            });
            var ships = new List<PlannedShip>(); var repeat = new List<PlannedShip>();
            bool sawA = false, sawB = false, sawBelowMaximum = false;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var rng = RngStreams.For(seed, RngPurpose.EncounterFleet, 0);
                int count = rng.NextInt(2, 6);
                EncounterComposer.Compose(def, seed, .5f, ships);
                EncounterComposer.Compose(def, seed, .5f, repeat);
                CollectionAssert.AreEqual(ships, repeat);
                Assert.That(ships.Count, Is.EqualTo(count));
                sawBelowMaximum |= count < 5;
                for (int k = 0; k < 5; k++)
                {
                    float u = rng.NextFloat();
                    if (k >= count) continue;
                    bool chooseA = u < .25f;
                    Assert.That(ships[k].Blueprint, Is.SameAs(chooseA ? a : b));
                    Assert.That(ships[k].Profile, Is.SameAs(chooseA ? fallback : own));
                    sawA |= chooseA; sawB |= !chooseA;
                }
            }
            Assert.That(sawA && sawB && sawBelowMaximum, Is.True);
        }

        [Test] public void CapTruncates()
        {
            var faction = TestContentFactory.Create<FactionDefinition>("faction_test");
            ShipBlueprintDefinition a = Blueprint("ship_a"), b = Blueprint("ship_b");
            var def = Explicit(faction,
                new EncounterShipEntry { blueprint = a, countMin = 2, countMax = 2 },
                new EncounterShipEntry { blueprint = b, countMin = 3, countMax = 3 });
            var full = new List<PlannedShip>(); var ships = new List<PlannedShip>();
            EncounterComposer.Compose(def, 7, .5f, full);
            Assert.That(full.Count, Is.EqualTo(5));
            // §5.5: Compose has no live-cap inputs. Its caller truncates the ordered list after all draws.
            foreach (int available in new[] { 0, 1, 3, 10 })
            {
                EncounterComposer.Compose(def, 7, .5f, ships);
                int keep = System.Math.Min(ships.Count, available);
                ships.RemoveRange(keep, ships.Count - keep);
                Assert.That(ships.Count, Is.EqualTo(System.Math.Min(5, available)));
                for (int i = 0; i < ships.Count; i++) Assert.That(ships[i], Is.EqualTo(full[i]));
            }
        }
    }
}
