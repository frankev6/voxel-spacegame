using System.Collections.Generic;
using Game.Core.Components;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public unsafe sealed class BlastDamageTests
    {
        static HashSet<int3> Crater(float amount, float occlusion)
        {
            using var f = new TestGridFactory(); var root = f.Spawn(f.Box(0, new int3(15))); var w = f.Writer(root);
            try
            {
                w.Config.OcclusionFactor = occlusion;
                w.Event = new DamageEvent { LocalPoint = new float3(7.5f) * .5f, Radius = 3 * .5f, Amount = amount };
                w.BlastDamage(); var removed = new HashSet<int3>(); foreach (var c in w.Removed) removed.Add(c.Cell); return removed;
            }
            finally { w.Dispose(); }
        }
        [Test] public void Crater_IsSymmetric_NoOcclusion()
        {
            var crater = Crater(400, 0); Assert.AreEqual(19, crater.Count);
            int[][] permutations = { new[] {0,1,2}, new[] {0,2,1}, new[] {1,0,2}, new[] {1,2,0}, new[] {2,0,1}, new[] {2,1,0} };
            foreach (var cell in crater) foreach (var permutation in permutations) for (int signs = 0; signs < 8; signs++)
            {
                var delta = cell - 7; var transformed = new int3(delta[permutation[0]], delta[permutation[1]], delta[permutation[2]]);
                for (int axis = 0; axis < 3; axis++) if ((signs & (1 << axis)) != 0) transformed[axis] *= -1;
                Assert.IsTrue(crater.Contains(transformed + 7), "All 48 cube symmetries preserve removed cells");
            }
        }
        [Test] public void Occlusion_ShieldsInterior()
        {
            // 400 damage only destroys immediate neighbours: no cells lie between them and the centre.
            // 1600 reaches the next layer and therefore exercises pre-event HP shielding (SPEC-13 Q27).
            var open = Crater(1600, 0); var shielded = Crater(1600, 1);
            Assert.Less(shielded.Count, open.Count); Assert.IsTrue(shielded.IsSubsetOf(open));
            Assert.IsTrue(open.Contains(new int3(9,7,7))); Assert.IsFalse(shielded.Contains(new int3(9,7,7)));
            TestContext.WriteLine($"radius-3 blast: open={open.Count}, occluded={shielded.Count}");
        }
        [Test] public void RadiusClamp_Warns()
        {
            using var f = new TestGridFactory(); var root = f.Spawn(f.Box(0, new int3(15)));
            var owner = f.World.CreateSystem<DamageStreamsSystem>(); var resolve = f.World.CreateSystem<DamageResolveSystem>();
            var clock = f.EM.CreateSingleton(new SimClock());
            using var configQuery = f.EM.CreateEntityQuery(typeof(DamageConfig)); var config = DamageConfig.Default;
            config.MaxBlastRadiusCells = 2; config.OcclusionFactor = 0; configQuery.SetSingleton(config);
            using var streamsQuery = f.EM.CreateEntityQuery(typeof(DamageStreams)); var streams = streamsQuery.GetSingleton<DamageStreams>();
            LogAssert.Expect(LogType.Warning, "[Damage] blast radius clamped to configured cell cap.");
            int warnings = 0;
            void Count(string message, string stack, LogType type)
            { if (type == LogType.Warning && message == "[Damage] blast radius clamped to configured cell cap.") System.Threading.Interlocked.Increment(ref warnings); }
            Application.logMessageReceivedThreaded += Count;
            try
            {
                for (uint tick = 1; tick <= 2; tick++)
                {
                    f.EM.SetComponentData(clock, new SimClock { Tick = tick }); owner.Update(f.World.Unmanaged);
                    streams.Damage.PostFromMainThread(new DamageEvent { Grid = root, LocalPoint = new float3(7.5f) * .5f, Radius = 100, Amount = 400 });
                    resolve.Update(f.World.Unmanaged); f.EM.CompleteAllTrackedJobs(); streams.Data->ResolverHandle.Complete();
                    if (tick == 1) Assert.Greater(streams.Data->Removed.Length, 0);
                    foreach (var c in streams.Data->Removed) Assert.Less(math.distance((float3)c.Cell + .5f, new float3(7.5f)), 2);
                }
            }
            finally { Application.logMessageReceivedThreaded -= Count; }
            Assert.AreEqual(1, warnings, "Repeated oversized events warn once per resolver lifetime");
            // The same radius handling must cap a sub-half-cell blast's penetration at one.
            var row = f.Spawn(f.Box(0, new int3(3,0,0))); var w = f.Writer(row);
            try
            {
                w.Event = new DamageEvent { LocalPoint = new float3(0,.25f,.25f), LocalDir = new float3(1,0,0), Radius = .1f, Amount = 1000, Penetration = 10 };
                w.BlastDamage(); Assert.AreEqual(1, w.Removed.Length);
            }
            finally { w.Dispose(); }
        }
    }
}
