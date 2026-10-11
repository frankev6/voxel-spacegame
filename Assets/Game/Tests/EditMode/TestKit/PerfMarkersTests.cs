using Game.Core.Diagnostics;
using NUnit.Framework;
using Unity.Profiling;

namespace Game.Tests.EditMode.TestKit
{
    /// <summary>TST-001: markers named "&lt;Type&gt;.&lt;Step&gt;" are discovered (SPEC-71 decision 7).</summary>
    public sealed class PerfMarkersTests
    {
        static readonly ProfilerMarker FooBar = new ProfilerMarker("Foo.Bar");
        static readonly ProfilerMarker NotGame = new ProfilerMarker("lowercase.marker");

        [TearDown] public void TearDown() => PerfMarkers.StopRecording();

        [Test]
        public void Discovers_NamedMarker()
        {
            using (FooBar.Auto()) { }
            using (NotGame.Auto()) { }
            PerfMarkers.StartRecording();
            Assert.IsTrue(PerfMarkers.TryGetLastFrameMs("Foo.Bar", out _), "Foo.Bar discovered");
            Assert.IsFalse(PerfMarkers.TryGetLastFrameMs("lowercase.marker", out _), "names outside the pattern are skipped");
            int count = PerfMarkers.Count;
            PerfMarkers.StartRecording();
            Assert.AreEqual(count, PerfMarkers.Count, "a second StartRecording adds nothing new");

            using (FooBar.Auto()) { var until = System.Diagnostics.Stopwatch.StartNew(); while (until.Elapsed.TotalMilliseconds < 2) { } }
            Assert.IsTrue(PerfMarkers.TryGetCurrentFrameMs("Foo.Bar", out var ms));
            UnityEngine.Debug.Log($"Foo.Bar current-frame time after a 2 ms span: {ms:F3} ms");
        }
    }
}
