using System;
using Game.Core;
using Game.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.Data.Logic
{
    /// <summary>NAR-001: hash-based fact identity and the thirteen canonical reference kinds (SPEC-40 §4.2–§4.4).</summary>
    [Category("Narrative")]
    public sealed class NarrativeContractTests
    {
        [Test]
        public void FactId_EqualityUsesHash()
        {
            var first = new FactId("intro_awake");
            var same = new FactId("intro_awake");
            var other = new FactId("story_survey_complete");
            Assert.IsTrue(first.IsValid);
            Assert.AreEqual("intro_awake", first.Name);
            Assert.AreEqual(first.Name, first.ToString());
            Assert.AreEqual(first.Id.Hash.Value, first.Hash);
            Assert.IsTrue(first.Equals(same));
            Assert.IsTrue(first.Equals((object)same));
            Assert.IsFalse(first.Equals(other));
            Assert.IsFalse(first.Equals((object)first.Name));
            Assert.AreEqual(unchecked((int)first.Hash), first.GetHashCode());
            Assert.IsFalse(default(FactId).IsValid);

            // Forge a collision in boxed test data: FactId equality must use the hash even when the source names differ.
            object collidingContent = other.Id;
            typeof(ContentId).GetField(nameof(ContentId.Hash)).SetValue(collidingContent, first.Id.Hash);
            object collidingFact = other;
            typeof(FactId).GetField(nameof(FactId.Id)).SetValue(collidingFact, collidingContent);
            var collision = (FactId)collidingFact;
            Assert.AreNotEqual(first.Name, collision.Name);
            Assert.AreEqual(first.Hash, collision.Hash);
            Assert.IsTrue(first.Equals(collision));
            Assert.IsTrue(first.Equals((object)collision));
            Assert.AreEqual(first.GetHashCode(), collision.GetHashCode());
        }

        [Test]
        public void RefStructs_HaveKindConstants()
        {
            var expected = new (Type Type, RefKind Kind)[]
            {
                (typeof(ItemRef), RefKind.Item),
                (typeof(EntityRef), RefKind.Entity),
                (typeof(StarSystemRef), RefKind.StarSystem),
                (typeof(QuestRef), RefKind.Quest),
                (typeof(ClueRef), RefKind.Clue),
                (typeof(BeatRef), RefKind.Beat),
                (typeof(FactionRef), RefKind.Faction),
                (typeof(MysteryRef), RefKind.Mystery),
                (typeof(LootTableRef), RefKind.LootTable),
                (typeof(MerchantRef), RefKind.Merchant),
                (typeof(EncounterRef), RefKind.Encounter),
                (typeof(BlueprintRef), RefKind.Blueprint),
                (typeof(AudioCueRef), RefKind.AudioCue)
            };
            foreach (var row in expected)
            {
                Assert.IsTrue(row.Type.IsValueType, row.Type.Name);
                Assert.IsTrue(Attribute.IsDefined(row.Type, typeof(SerializableAttribute)), row.Type.Name);
                var kind = row.Type.GetField("Kind");
                Assert.IsNotNull(kind, row.Type.Name);
                Assert.IsTrue(kind.IsLiteral, row.Type.Name);
                Assert.AreEqual(typeof(RefKind), kind.FieldType, row.Type.Name);
                Assert.AreEqual((byte)row.Kind, Convert.ToByte(kind.GetRawConstantValue()), row.Type.Name);
                Assert.AreEqual(typeof(string), row.Type.GetField("Id").FieldType, row.Type.Name);
                var index = row.Type.GetField("Index");
                Assert.AreEqual(typeof(int), index.FieldType, row.Type.Name);
                Assert.IsTrue(Attribute.IsDefined(index, typeof(NonSerializedAttribute)), row.Type.Name);
                Assert.AreEqual(0, index.GetValue(Activator.CreateInstance(row.Type)), row.Type.Name);
            }
        }
    }
}
