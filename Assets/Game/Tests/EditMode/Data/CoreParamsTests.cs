using Game.Data.Blocks;
using NUnit.Framework;

namespace Game.Tests.EditMode.Data
{
    /// <summary>Core floor (owner, 2026-10-18): a core holds its MaxCells at most and malfunctions below 20% of it unless MinCells says otherwise.</summary>
    public sealed class CoreParamsTests
    {
        [Test] public void Floor_DefaultsToTwentyPercent()
        {
            var core = new CoreParams { MaxCells = 4000, MaxExtentCells = 100 };
            Assert.AreEqual(800, core.Floor);
            Assert.IsTrue(core.Fits(800)); Assert.IsTrue(core.Fits(4000));
            Assert.IsFalse(core.Fits(799)); Assert.IsFalse(core.Fits(4001));
        }
        [Test] public void Floor_ExplicitMinCellsWins()
        {
            var core = new CoreParams { MaxCells = 4000, MaxExtentCells = 100, MinCells = 50 };
            Assert.AreEqual(50, core.Floor); Assert.IsTrue(core.Fits(50)); Assert.IsFalse(core.Fits(49));
        }
    }
}
