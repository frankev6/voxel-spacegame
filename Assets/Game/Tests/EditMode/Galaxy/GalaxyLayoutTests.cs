using System.Linq;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Generation;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class GalaxyLayoutTests
    {
        NativeLeakDetectionMode previousLeakMode;
        [SetUp] public void SetUp()
        { previousLeakMode = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace; }
        [TearDown] public void TearDown()
        { TestContentFactory.DestroyAll(); NativeLeakDetection.Mode = previousLeakMode; }
        static GalaxyDef Build(TestGalaxyFixtures.Fixture fixture)
            => GalaxyDefBuilder.Build(ContentCatalog.Build(fixture.Definitions, new ValidationReport("GalaxyLayoutTests")), fixture.Generation.FixedSeed, 1);

        [Test]
        public unsafe void SameSeed_Identical()
        {
            using var fixture = TestGalaxyFixtures.Small;
            int original = JobsUtility.JobWorkerCount;
            try
            {
                JobsUtility.JobWorkerCount = 1;
                using var first = Build(fixture);
                using var repeat = Build(fixture);
                JobsUtility.JobWorkerCount = JobsUtility.JobWorkerMaximumCount;
                using var parallel = Build(fixture);
                Assert.That(repeat.StarSystems.Length, Is.EqualTo(first.StarSystems.Length));
                Assert.That(parallel.StarSystems.Length, Is.EqualTo(first.StarSystems.Length));
                long size = (long)first.StarSystems.Length * UnsafeUtility.SizeOf<StarSystemDef>();
                Assert.That(UnsafeUtility.MemCmp(first.StarSystems.GetUnsafeReadOnlyPtr(), repeat.StarSystems.GetUnsafeReadOnlyPtr(), size), Is.Zero);
                Assert.That(UnsafeUtility.MemCmp(first.StarSystems.GetUnsafeReadOnlyPtr(), parallel.StarSystems.GetUnsafeReadOnlyPtr(), size), Is.Zero);
            }
            finally { JobsUtility.JobWorkerCount = original; }
        }

        [Test]
        public void Spacing_Respected()
        {
            using var fixture = TestGalaxyFixtures.Small;
            using var galaxy = Build(fixture);
            Assert.That(galaxy.SkippedProceduralCount, Is.Zero);
            for (int i = 0; i < galaxy.StarSystems.Length; i++)
            {
                var a = galaxy.StarSystems[i];
                Assert.That(math.length(a.PositionLy), Is.LessThanOrEqualTo(fixture.Generation.GalaxyRadiusLy));
                for (int j = 0; j < i; j++)
                    Assert.That(math.distance(a.PositionLy, galaxy.StarSystems[j].PositionLy), Is.GreaterThanOrEqualTo(fixture.Generation.MinStarSystemSpacingLy));
            }
        }

        [Test]
        public void Ids_Structural()
        {
            using var fixture = TestGalaxyFixtures.Small;
            using var galaxy = Build(fixture);
            uint next = 1;
            foreach (var system in galaxy.StarSystems)
            {
                Assert.That(galaxy.TryGetIndex(system.Id, out int index), Is.True);
                Assert.That(galaxy.Get(system.Id).Id, Is.EqualTo(galaxy.StarSystems[index].Id));
                if (system.AuthoredIndex == 0) Assert.That(system.Id.Value, Is.EqualTo(next++));
                else Assert.That(system.Id.IsAuthored, Is.True);
            }
        }

        [Test]
        public void AuthoredEdit_DoesNotMoveProcedural()
        {
            using var fixture = TestGalaxyFixtures.Small;
            using var before = Build(fixture);
            TestContentFactory.Edit(fixture.Start, so => TestContentFactory.Set(so, "danger", 3));
            TestContentFactory.Edit(fixture.Generation, so =>
            {
                var pool = so.FindProperty("templatePool");
                int index = pool.arraySize++;
                pool.GetArrayElementAtIndex(index).FindPropertyRelative("template").objectReferenceValue = fixture.Template;
                pool.GetArrayElementAtIndex(index).FindPropertyRelative("weight").floatValue = 0;
            });
            using var after = Build(fixture);
            CollectionAssert.AreEqual(before.StarSystems.ToArray().Where(x => x.AuthoredIndex == 0).ToArray(),
                after.StarSystems.ToArray().Where(x => x.AuthoredIndex == 0).ToArray());
        }

        [Test]
        public void Dispose_NoLeaks()
        {
            using var fixture = TestGalaxyFixtures.Small;
            using (var warmup = Build(fixture)) { }
            UnsafeUtility.CheckForLeaks();
            var galaxy = Build(fixture);
            galaxy.Dispose(); galaxy.Dispose();
            Assert.That(galaxy.StarSystems.IsCreated || galaxy.IndexById.IsCreated || galaxy.PoolEntries.IsCreated || galaxy.AuthoredBodies.IsCreated, Is.False);
            UnsafeUtility.CheckForLeaks();
        }
    }
}
