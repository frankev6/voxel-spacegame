using Game.Presentation.Vfx;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Tests.EditMode.Vfx
{
    /// <summary>SPEC-63 §4 fixed event and GPU layouts.</summary>
    public sealed class FxContractTests
    {
        [Test] public void FxEvent_Size_Is56() => Assert.AreEqual(56, UnsafeUtility.SizeOf<FxEvent>());
        [Test] public void FxGpuEvent_Size_Is80() => Assert.AreEqual(80, UnsafeUtility.SizeOf<FxGpuEvent>());
        [Test] public void InstanceStructs_HaveShaderSizes()
        {
            Assert.AreEqual(48, UnsafeUtility.SizeOf<TracerInstance>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<BeamInstance>());
            Assert.AreEqual(48, UnsafeUtility.SizeOf<PlumeInstance>());
            Assert.AreEqual(224, UnsafeUtility.SizeOf<ShieldInstance>());
        }
    }
}
