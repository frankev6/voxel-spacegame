using Game.Presentation.Audio;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class SfxVoiceAllocatorTests
    {
        [Test] public void PerCueCap_StealsOldestOnlyIfLouder()
        {
            var voices = new VoiceInfo[3];
            voices[0] = new VoiceInfo { Active = true, Slot = 1, StartTime = 2, Audibility = .2f };
            voices[1] = new VoiceInfo { Active = true, Slot = 1, StartTime = 1, Audibility = .5f };
            var cue = new SfxCueInfo { MaxVoices = 2 }; var candidate = new SfxCandidate { Slot = 1, Audibility = .5f };
            Assert.AreEqual(-1, SfxVoiceAllocator.Select(candidate, voices, cue, out _));
            candidate.Audibility = .6f;
            Assert.AreEqual(1, SfxVoiceAllocator.Select(candidate, voices, cue, out bool stolen)); Assert.IsTrue(stolen);
        }
        [Test] public void PoolFull_StealsLowerPriority()
        {
            var voices = new[] { new VoiceInfo { Active = true, Slot = 2, Priority = 100, Audibility = 1 },
                new VoiceInfo { Active = true, Slot = 2, Priority = 200, Audibility = 1 } };
            var cue = new SfxCueInfo { Priority = 150, MaxVoices = 2 };
            Assert.AreEqual(1, SfxVoiceAllocator.Select(new SfxCandidate { Slot = 1, Audibility = .1f }, voices, cue, out bool stolen));
            Assert.IsTrue(stolen); cue.Priority = 250;
            Assert.AreEqual(-1, SfxVoiceAllocator.Select(new SfxCandidate { Slot = 1, Audibility = 1 }, voices, cue, out _));
        }
        [Test] public void PoolFull_EqualPriorityLouderWins()
        {
            var voices = new[] { new VoiceInfo { Active = true, Slot = 2, Priority = 100, Audibility = .4f, StartTime = 2 },
                new VoiceInfo { Active = true, Slot = 2, Priority = 100, Audibility = .4f, StartTime = 1 } };
            var cue = new SfxCueInfo { Priority = 100, MaxVoices = 2 }; var candidate = new SfxCandidate { Slot = 1, Audibility = .4f };
            Assert.AreEqual(-1, SfxVoiceAllocator.Select(candidate, voices, cue, out _)); candidate.Audibility = .5f;
            Assert.AreEqual(1, SfxVoiceAllocator.Select(candidate, voices, cue, out bool stolen)); Assert.IsTrue(stolen);
        }
    }
}
