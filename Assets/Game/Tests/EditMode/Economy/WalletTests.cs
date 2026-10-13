using System;
using Game.Economy;
using NUnit.Framework;

namespace Game.Tests.EditMode.Economy
{
    public sealed class WalletTests
    {
        [Test] public void Saturates()
        {
            var wallet = new Wallet(); int changes = 0; wallet.Changed += () => changes++;
            wallet.Add(2000); Assert.AreEqual(2000, wallet.Credits);
            wallet.Add(long.MaxValue); Assert.AreEqual(Wallet.MaxCredits, wallet.Credits);
            wallet.Add(1); Assert.AreEqual(Wallet.MaxCredits, wallet.Credits); Assert.AreEqual(2, changes);
            wallet.Set(-1); Assert.AreEqual(0, wallet.Credits);
            wallet.Set(long.MaxValue); Assert.AreEqual(Wallet.MaxCredits, wallet.Credits);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(-1));
            Assert.AreEqual(Wallet.MaxCredits, wallet.Credits); Assert.AreEqual(4, changes);
        }

        [Test] public void Spend_AllOrNothing()
        {
            var wallet = new Wallet(); wallet.Set(2000);
            int changes = 0; wallet.Changed += () => changes++;
            Assert.IsFalse(wallet.TrySpend(2001)); Assert.AreEqual(2000, wallet.Credits); Assert.AreEqual(0, changes);
            Assert.IsTrue(wallet.TrySpend(0)); Assert.AreEqual(0, changes);
            Assert.IsTrue(wallet.TrySpend(500)); Assert.AreEqual(1500, wallet.Credits);
            Assert.IsTrue(wallet.TrySpend(1500)); Assert.AreEqual(0, wallet.Credits); Assert.AreEqual(2, changes);
            Assert.IsFalse(wallet.TrySpend(1)); Assert.AreEqual(0, wallet.Credits);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1)); Assert.AreEqual(2, changes);
        }
    }
}
