using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.TestTools;

namespace Game.Tests.Common
{
    /// <summary>NUnit base for EditMode tests: leak detection with stack traces, the error-log guard and tracked worlds.
    /// Implements SPEC-71 §4.1, §5.2.</summary>
    public abstract class GameTestBase
    {
        readonly List<EcsTestWorld> worlds = new List<EcsTestWorld>();
        NativeLeakDetectionMode previousLeakMode;

        [SetUp]
        public void GameTestSetUp()
        {
            previousLeakMode = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            LogAssert.ignoreFailingMessages = false;
        }

        [TearDown]
        public void GameTestTearDown()
        {
            try { DisposeTrackedWorlds(); }
            finally
            {
                NativeLeakDetection.Mode = previousLeakMode;
            }
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Disposes every world from CreateWorld (teardown does this).</summary>
        protected void DisposeTrackedWorlds()
        {
            foreach (var w in worlds) w.Dispose();
            worlds.Clear();
        }

        /// <summary>Tracked: disposed in teardown even when the test throws.</summary>
        protected EcsTestWorld CreateWorld(params System.Type[] systems)
        {
            var w = EcsTestWorld.Create(systems);
            worlds.Add(w);
            return w;
        }
    }
}
