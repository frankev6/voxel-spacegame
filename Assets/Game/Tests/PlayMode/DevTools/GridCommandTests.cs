using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.Core.Components;
using NUnit.Framework;

namespace Game.Tests.PlayMode.DevTools
{
    public sealed class GridCommandTests
    {
        [Test] public void PrintsAllFields()
        {
            using var f = new WorldCommandFixture(); var result = f.Command("grid player=true bricks=true");
            Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            foreach (string field in new[] { "PersistentId=", "Entity=", "GridKind=", "Hulk=", "Dormant=", "CellSize=", "BrickMin=", "BrickDims=", "Revision=",
                "bricks=", "solid cells=", "block instances=", "dirty bricks: mesh=", "collider=", "both=", "applied meshes=", "mass=", "linear speed=", "angular speed=", "UniversePosition=" })
                StringAssert.Contains(field, result.Text);
            StringAssert.Contains("solid cells=1", result.Text); StringAssert.Contains("brick int3", result.Text);
            ulong id = f.Em.GetComponentData<PersistentId>(f.Player).Value;
            Assert.AreEqual(DevResultKind.Ok, f.Command("grid id=" + id).Kind);
            Assert.AreEqual(DevResultKind.Failed, f.Command("grid id=18446744073709551615").Kind);
            Assert.IsTrue(f.Host.Probes.TryGet("grid.exists", out var exists));
            Assert.IsTrue(exists.Read(f.Host.Context, new[] { ScenarioExpression.Number(id) }).Bool);
            Assert.IsFalse(exists.Read(f.Host.Context, new[] { ScenarioExpression.Text("18446744073709551615") }).Bool);
            Assert.IsTrue(f.Host.Probes.TryGet("player.cells", out var cells));
            Assert.AreEqual(1, cells.Read(f.Host.Context, System.ReadOnlySpan<ScenarioValue>.Empty).Number);
            Assert.IsTrue(f.Host.Probes.TryGet("grids.count", out var count));
            Assert.AreEqual(1, count.Read(f.Host.Context, new[] { ScenarioExpression.Text("Ship") }).Number);
            Assert.AreEqual(DevResultKind.Failed, f.Command("grid player=true nearest=true").Kind);
        }
    }
}
