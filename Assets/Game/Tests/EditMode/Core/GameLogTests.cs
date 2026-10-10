using Game.Core;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Core
{
    public sealed class GameLogTests
    {
        const string Category = "GameLogTests";

        [SetUp]
        public void SetUp()
        {
            GameLog.ResetOnceKeys();
            GameLog.SetMinimumLevel(Category, LogLevel.Info);
        }

        [Test]
        public void Info_FormatsCategory()
        {
            LogAssert.Expect(LogType.Log, "[GameLogTests] hello");
            LogAssert.Expect(LogType.Warning, "[GameLogTests] careful");
            LogAssert.Expect(LogType.Error, "[GameLogTests] broken");
            GameLog.Info(Category, "hello");
            GameLog.Warn(Category, "careful");
            GameLog.Error(Category, "broken");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WarnOnce_LogsOnce_CountsAll()
        {
            LogAssert.Expect(LogType.Warning, "[GameLogTests] repeated");
            int logged = 0;
            for (int i = 0; i < 100; i++)
                if (GameLog.WarnOnce("gamelog-test-key", Category, "repeated")) logged++;
            Assert.AreEqual(1, logged);
            Assert.AreEqual(100, GameLog.GetOnceCount("gamelog-test-key"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CategoryLevel_FiltersBelow()
        {
            GameLog.SetMinimumLevel(Category, LogLevel.Error);
            LogAssert.Expect(LogType.Error, "[GameLogTests] kept");
            LogAssert.Expect(LogType.Error, "[GameLogTests] ctx: InvalidOperationException: boom");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            GameLog.Info(Category, "dropped");
            GameLog.Warn(Category, "dropped");
            GameLog.Error(Category, "kept");
            GameLog.Exception(Category, new System.InvalidOperationException("boom"), "ctx");
            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(LogLevel.Error, GameLog.GetMinimumLevel(Category));
            Assert.AreEqual(GameLog.DefaultMinimumLevel, GameLog.GetMinimumLevel("GameLogTests.Unset"));
        }

        [Test]
        public void Exception_LogsContextAndException()
        {
            LogAssert.Expect(LogType.Error, "[GameLogTests] loading slot 2: ArgumentException: bad slot");
            LogAssert.Expect(LogType.Exception, "ArgumentException: bad slot");
            GameLog.Exception(Category, new System.ArgumentException("bad slot"), "loading slot 2");
            LogAssert.NoUnexpectedReceived();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct BurstErrorJob : IJob
        {
            public void Execute()
            {
                FixedString128Bytes message = "[GameLogTests] from burst";   // literal conversion is Burst-compatible
                GameLog.Burst.Error(message);
            }
        }

        [Test]
        public void BurstError_LogsFromJob()
        {
            LogAssert.Expect(LogType.Error, "[GameLogTests] from burst");
            new BurstErrorJob().Schedule().Complete();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ResetOnceKeys_AllowsLogAgain()
        {
            LogAssert.Expect(LogType.Error, "[GameLogTests] once");
            LogAssert.Expect(LogType.Error, "[GameLogTests] once");
            Assert.IsTrue(GameLog.ErrorOnce("gamelog-reset-key", Category, "once"));
            Assert.IsFalse(GameLog.ErrorOnce("gamelog-reset-key", Category, "once"));
            GameLog.ResetOnceKeys();
            Assert.AreEqual(0, GameLog.GetOnceCount("gamelog-reset-key"));
            Assert.IsTrue(GameLog.ErrorOnce("gamelog-reset-key", Category, "once"));
            LogAssert.Expect(LogType.Warning, "[GameLogTests] null key");
            Assert.IsTrue(GameLog.WarnOnce(null, Category, "null key"));
            Assert.AreEqual(1, GameLog.GetOnceCount("<null>"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
