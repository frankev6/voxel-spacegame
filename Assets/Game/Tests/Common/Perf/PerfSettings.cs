using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;

namespace Game.Tests.Common.Perf
{
    /// <summary>The 06 reference settings for perf runs (SPEC-71 decision 4, §5.3): Burst on, Burst safety checks off, job debugger off,
    /// leak detection off. Anything else makes a perf test inconclusive, never failed.</summary>
    public static class PerfSettings
    {
        /// <summary>Assert.Inconclusive with the reason when the settings are not the reference ones.</summary>
        public static void AssertReference()
        {
            if (!IsReference(out var reason)) Assert.Inconclusive("not the reference perf settings: " + reason);
        }

        public static bool IsReference(out string reason)
        {
            var problems = new System.Collections.Generic.List<string>();
            if (!BurstCompiler.IsEnabled) problems.Add("Burst is off");
            if (BurstCompiler.Options.EnableBurstSafetyChecks) problems.Add("Burst safety checks are on");
            if (JobsUtility.JobDebuggerEnabled) problems.Add("the job debugger is on");
            if (NativeLeakDetection.Mode != NativeLeakDetectionMode.Disabled) problems.Add("leak detection is " + NativeLeakDetection.Mode);
            reason = string.Join("; ", problems);
            return problems.Count == 0;
        }
    }
}
