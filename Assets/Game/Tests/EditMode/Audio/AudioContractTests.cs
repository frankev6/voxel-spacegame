using System;
using Game.Core;
using Game.Core.Audio;
using Game.Presentation.Audio;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Audio
{
    /// <summary>AUD-001 contract checks (SPEC-64 §4.1, §4.2, §6.1).</summary>
    public sealed class AudioContractTests
    {
        [Test]
        public void SfxEvent_Size_Is32()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<SfxEvent>());
            var e = SfxEvents.At(new ContentHash(42), new float3(1, 2, 3), 1.7f, player: true);
            Assert.AreEqual(1f, e.Volume, "volume saturates");
            Assert.AreEqual(SfxEventFlags.PlayerSource, e.Flags);
            Assert.AreEqual(0f, SfxEvents.At(new ContentHash(42), float3.zero, -1f).Volume);
            Assert.AreEqual(11, Enum.GetValues(typeof(UiSound)).Length);
            Assert.AreEqual(0.8f, AudioVolumes.Default.Master);
        }

        [Test]
        public void AudioService_ConstructsSilentCoreWithoutConfig()
        {
            using var svc = new AudioService(null, null, AudioVolumes.Default, null);
            svc.ApplyVolumes(AudioVolumes.Default); svc.PlayUi(UiSound.Click); svc.PlayCue(default); svc.ClearMusicOverride(1f);
            Assert.AreEqual(MusicState.Silent, svc.MusicState);
            Assert.IsNotNull(svc.Stats);
        }
    }
}
