using System.IO;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class GalaxyHashTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        [Test] public void Deterministic()
        {
            using var fixture = TestGalaxyFixtures.Small;
            using var first = GalaxyLaneTests.Build(fixture); using var second = GalaxyLaneTests.Build(fixture);
            Assert.That(first.Hash, Is.Not.Zero);
            Assert.That(second.Hash, Is.EqualTo(first.Hash));
        }
        [Test] public void MatchesGolden()
        {
            using var fixture = TestGalaxyFixtures.Small;
            using var galaxy = GalaxyLaneTests.Build(fixture);
            string path = Path.Combine(Application.dataPath, "Game/Tests/EditMode/Galaxy/Golden/galaxy_v1.txt");
            Assert.That(galaxy.Hash.ToString("X16"), Is.EqualTo(File.ReadAllText(path).Trim()));
        }
    }
}
