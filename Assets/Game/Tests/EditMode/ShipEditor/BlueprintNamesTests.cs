using System.Collections.Generic;
using Game.ShipEditor.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class BlueprintNamesTests
    {
        [Test] public void Sanitize_Rules()
        {
            Assert.AreEqual("my_ship_mk-2", BlueprintNames.Sanitize("  My / Ship  Mk-2  "));
            Assert.AreEqual("blueprint", BlueprintNames.Sanitize(null)); Assert.AreEqual("blueprint", BlueprintNames.Sanitize("/..\\"));
            Assert.AreEqual(new string('a', 48), BlueprintNames.Sanitize(new string('A', 100)));
            Assert.IsTrue(BlueprintNames.IsId(BlueprintNames.Sanitize("../ship ? 雪")));
            Assert.IsFalse(BlueprintNames.IsId("../ship")); Assert.IsFalse(BlueprintNames.IsId("ship/other")); Assert.IsFalse(BlueprintNames.IsId(new string('a', 49)));
        }
        [Test] public void Collision_Suffix()
        {
            var used = new HashSet<string> { "my_ship", "my_ship_2" };
            Assert.AreEqual("my_ship_3", BlueprintNames.Unique("My Ship", used.Contains));
            string full = new string('a', 48); used.Add(full); used.Add(new string('a', 46) + "_2");
            string next = BlueprintNames.Unique(full, used.Contains); Assert.AreEqual(new string('a', 46) + "_3", next); Assert.AreEqual(48, next.Length);
            Assert.AreEqual("new_ship", BlueprintNames.Unique("New Ship", used.Contains));
        }
    }
}
