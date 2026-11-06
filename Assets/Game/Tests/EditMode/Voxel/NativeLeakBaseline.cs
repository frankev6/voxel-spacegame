using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>Establishes a leak-tracking interval before test-owned allocations; final probes remain strict.</summary>
    internal static class NativeLeakBaseline
    {
        internal static void Capture()
        {
            // Matches ShipDeathRegressionTests.BaselineLeaks: this first synchronous probe reports
            // pre-existing engine/fixture allocations and resets tracking. Never use at final disposal.
            void BaselineLog(string message, string trace, LogType type)
            {
                if (type == LogType.Log && message.StartsWith("Leak Detected : ", StringComparison.Ordinal))
                    LogAssert.Expect(type, message);
            }
            LogAssert.NoUnexpectedReceived();
            Application.logMessageReceived += BaselineLog;
            try { UnsafeUtility.CheckForLeaks(); }
            finally { Application.logMessageReceived -= BaselineLog; }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
