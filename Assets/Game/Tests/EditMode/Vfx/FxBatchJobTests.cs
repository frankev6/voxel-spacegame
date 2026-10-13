using Game.Core;
using Game.Data.Definitions;
using Game.Presentation.Vfx;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Game.Tests.EditMode.Vfx
{
    public sealed class FxBatchJobTests
    {
        static NativeArray<FxEvent> Events(FxKind kind, int count = 1, float distance = 0, FxEventFlags flags = 0)
        {
            var events = new NativeArray<FxEvent>(count, Allocator.TempJob);
            for (int i = 0; i < count; i++) events[i] = new FxEvent { Kind = kind, Pos = new float3(distance, 0, 0), Scale = 1, Flags = flags };
            return events;
        }
        [Test] public void UnknownHash_FallsBackToKindDefault()
        {
            using var f = new FxUploadFixture(); var e = Events(FxKind.HitKinetic);
            try { var item = e[0]; item.Fx = new ContentHash(999); e[0] = item; f.Job(e).Run(); Assert.AreEqual(1, f.Counts[1]); Assert.AreEqual(0, f.Counters[(int)FxCounter.Unresolved]); }
            finally { e.Dispose(); }
        }
        [Test] public void StateKind_IsRejected()
        { using var f = new FxUploadFixture(FxKind.Tracer); using var e = Events(FxKind.Tracer); f.Job(e).Run(); Assert.AreEqual(1, f.Counters[(int)FxCounter.StateKindRejected]); Assert.AreEqual(0, f.Counts[1]); }
        [Test] public void BeyondCullDistance_IsCulled()
        { using var f = new FxUploadFixture(); using var e = Events(FxKind.HitKinetic, distance: 1001); f.Job(e).Run(); Assert.AreEqual(1, f.Counters[(int)FxCounter.Culled]); }
        [Test] public void PlayerSource_IsNeverCulled()
        { using var f = new FxUploadFixture(); using var e = Events(FxKind.HitKinetic, distance: 1001, flags: FxEventFlags.PlayerSource); f.Job(e).Run(); Assert.AreEqual(1, f.Counts[1]); Assert.AreEqual(2, f.Output[0].particleCount); Assert.AreEqual(1.6f, f.Output[0].size); }
        [Test] public void FarDebris_KeepsOneInFour_AndScales()
        { using var f = new FxUploadFixture(FxKind.Debris); using var e = Events(FxKind.Debris, 8, 151); f.Job(e).Run(); Assert.AreEqual(2, f.Counts[1]); Assert.AreEqual(6, f.Counters[(int)FxCounter.Culled]); Assert.AreEqual(1.6f, f.Output[0].size); Assert.AreEqual(1, f.Output[0].particleCount); }
        [Test] public void DebrisBudget_StopsAtCap()
        { using var f = new FxUploadFixture(FxKind.Debris); using var e = Events(FxKind.Debris, 8); f.Job(e, 3).Run(); Assert.AreEqual(3, f.Counts[1]); Assert.AreEqual(3, f.Counters[(int)FxCounter.DebrisSpawned]); Assert.AreEqual(5, f.Counters[(int)FxCounter.BudgetDropped]); }
        [Test] public void SlotCap_DropsOverflow()
        {
            using var f = new FxUploadFixture(FxKind.Debris); var slot = f.Data.Slots[1]; slot.MaxEventsPerFrame = 2; f.Data.Slots[1] = slot;
            using var e = Events(FxKind.Debris, 8); f.Job(e).Run(); Assert.AreEqual(2, f.Counts[1]); Assert.AreEqual(6, f.Counters[(int)FxCounter.BudgetDropped]); Assert.AreEqual(2, f.Counters[(int)FxCounter.DebrisSpawned]);
        }
        [Test] public void PaletteFlag_ResolvesPaletteIndex()
        {
            using var f = new FxUploadFixture(FxKind.Debris); f.Data.Palette[0] = 0xff804020;
            using var e = Events(FxKind.Debris, flags: FxEventFlags.PaletteColor); f.Job(e).Run();
            Assert.That(f.Output[0].color.x, Is.EqualTo(32f / 255).Within(.00001)); Assert.That(f.Output[0].color.z, Is.EqualTo(128f / 255).Within(.00001));
        }
        [Test] public void NaNPosition_IsSkipped()
        { using var f = new FxUploadFixture(); using var e = Events(FxKind.HitKinetic, distance: float.NaN); f.Job(e).Run(); Assert.AreEqual(1, f.Counters[(int)FxCounter.Culled]); Assert.AreEqual(0, f.Counts[1]); }
    }
}
