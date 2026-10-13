using Game.Core;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class SfxCoalesceJobTests
    {
        SfxCoalesceJob job;
        [SetUp] public void SetUp()
        {
            job = new SfxCoalesceJob {
                Events = new NativeList<SfxEvent>(512, Allocator.TempJob),
                Slots = new NativeHashMap<ulong,int>(4, Allocator.TempJob),
                Cues = new NativeArray<SfxCueInfo>(4, Allocator.TempJob),
                LastStart = new NativeHashMap<ulong,double>(512, Allocator.TempJob),
                Candidates = new NativeList<SfxCandidate>(512, Allocator.TempJob),
                Groups = new NativeHashMap<ulong,int>(512, Allocator.TempJob),
                Unknown = new NativeList<ulong>(512, Allocator.TempJob),
                Counters = new NativeArray<int>((int)SfxCounter.Count, Allocator.TempJob), Now = 10, CombatRadius = 100 };
            for (int i = 1; i <= 3; i++) {
                job.Slots.Add((ulong)i, i);
                job.Cues[i] = new SfxCueInfo { Slot = i, Priority = 100, Volume = 1, SpatialBlend = 1,
                    MinDistance = 1, MaxDistance = 100, CoalesceCellSize = 16, CoalesceGain = .1f,
                    Coalesce = CoalesceMode.PerCell, MaxVoices = 40, Cooldown = .5f };
            }
        }
        [TearDown] public void TearDown()
        {
            job.Events.Dispose(); job.Slots.Dispose(); job.Cues.Dispose(); job.LastStart.Dispose();
            job.Candidates.Dispose(); job.Groups.Dispose(); job.Unknown.Dispose(); job.Counters.Dispose();
        }
        void Add(ulong cue = 1, float x = 0, float volume = 1, SfxEventFlags flags = 0)
            => job.Events.Add(new SfxEvent { Cue = new ContentHash(cue), Pos = new float3(x,0,0), Volume = volume, Flags = flags });
        [Test] public void UnknownHash_Unresolved()
        {
            Add(0); Add(99); job.Run();
            Assert.AreEqual(2, job.Counters[(int)SfxCounter.Unresolved]); Assert.AreEqual(0, job.Candidates.Length);
            Assert.AreEqual(1, job.Unknown.Length); Assert.AreEqual(99UL, job.Unknown[0]);
        }
        [Test] public void BeyondMaxDistance_Culled()
        { Add(x: 101); job.Run(); Assert.AreEqual(1, job.Counters[(int)SfxCounter.Culled]); Assert.AreEqual(0, job.Candidates.Length); }
        [Test] public void PlayerSource_NotCulled()
        { Add(x: 101, flags: SfxEventFlags.PlayerSource); job.Run(); Assert.AreEqual(0, job.Counters[(int)SfxCounter.Culled]); Assert.AreEqual(1, job.Candidates.Length); }
        [Test] public void SameCell_CoalescesToLoudest()
        {
            for (int i = 0; i < 199; i++) Add(x: 1, volume: .1f);
            Add(x: 7, volume: .5f); job.Run();
            Assert.AreEqual(1, job.Candidates.Length); var value = job.Candidates[0];
            Assert.AreEqual(200, value.Count); Assert.AreEqual(new float3(7,0,0), value.Pos);
            Assert.AreEqual(math.min(1, .5f * (1 + .1f * math.log2(200))), value.FinalVolume, .00001f);
        }
        [Test] public void DifferentCells_SeparateGroups()
        { Add(x: -1); Add(x: 0); Add(x: 16); job.Run(); Assert.AreEqual(3, job.Candidates.Length); }
        [Test] public void Cooldown_DropsRetrigger()
        {
            Add(); job.LastStart.Add(SfxCoalesceJob.GroupKey(1, job.Cues[1], float3.zero, 0), 9.75);
            job.Run(); Assert.AreEqual(0, job.Candidates.Length); Assert.AreEqual(1, job.Counters[(int)SfxCounter.CooldownDropped]);
            job.Now = 10.25; job.Run(); Assert.AreEqual(1, job.Candidates.Length);
        }
        [Test] public void Sort_PriorityThenPlayerThenAudibility()
        {
            for (int i = 1; i <= 3; i++) { var cue = job.Cues[i]; cue.Coalesce = CoalesceMode.None; if (i == 2) cue.Priority = 50; job.Cues[i] = cue; }
            Add(volume: .8f); Add(volume: .2f, flags: SfxEventFlags.PlayerSource); Add(2, volume: .01f);
            Add(3, volume: .9f); Add(3, volume: .9f); job.Run();
            int[] expected = { 2,1,3,4,0 };
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], job.Candidates[i].Order);
        }
    }
}
