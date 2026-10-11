using Game.Presentation.Audio;
using NUnit.Framework;
namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class VolumeMathTests
    {
        [Test] public void ToDb_KnownValues()
        {
            Assert.AreEqual(-80, VolumeMath.ToDb(0)); Assert.AreEqual(-80, VolumeMath.ToDb(.0001f));
            Assert.AreEqual(-6.0206f, VolumeMath.ToDb(.5f), .01f); Assert.AreEqual(0, VolumeMath.ToDb(1));
        }
    }
}
