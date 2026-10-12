using Game.UI.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    /// <summary>UI-006: SPEC-60 §5.6 cached invariant strings and two-digit clock fields.</summary>
    public sealed class NumberStringCacheTests
    {
        [Test] public void Get_InRange_SameInstance()
        {
            for (int i = -9999; i <= 9999; i++)
            {
                string first = NumberStringCache.Get(i);
                Assert.AreSame(first, NumberStringCache.Get(i));
                Assert.AreEqual(i.ToString(System.Globalization.CultureInfo.InvariantCulture), first);
            }
        }
        [Test] public void Get_OutOfRange_Correct()
        {
            Assert.AreEqual("10000", NumberStringCache.Get(10000));
            Assert.AreEqual("-10000", NumberStringCache.Get(-10000));
            Assert.AreEqual("2147483647", NumberStringCache.Get(int.MaxValue));
            Assert.AreEqual("-2147483648", NumberStringCache.Get(int.MinValue));
        }
        [Test] public void Get2Digits_PadsZero()
        {
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), NumberStringCache.Get2Digits(i));
                Assert.AreSame(NumberStringCache.Get2Digits(i), NumberStringCache.Get2Digits(i));
            }
        }
    }
}
