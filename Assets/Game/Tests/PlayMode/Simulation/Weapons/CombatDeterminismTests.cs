using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.PlayMode.Simulation.Weapons
{
    [Category("Scenario")]
    public sealed class CombatDeterminismTests
    {
        static (ulong hash, ProjectileStoreStats stats) Run(int fps, int workers)
        {
            int previous = JobsUtility.JobWorkerCount; JobsUtility.JobWorkerCount = workers;
            try
            {
                using var f = new CombatScenarioTests.Fixture();
                var roots = new Entity[4];
                for (int i = 0; i < 4; i++)
                    roots[i] = f.Ship("weapon_plasma_turret_small",new float3((i % 2) * 24,0,(i / 2) * 30));
                f.Ready(roots);
                for (int i = 0; i < 4; i++)
                {
                    if (i >= 2)
                    { var pose = f.Em.GetComponentData<LocalTransform>(roots[i]); pose.Rotation = quaternion.RotateY(math.PI); f.Em.SetComponentData(roots[i],pose); }
                    var target = roots[(i + 2) % 4];
                    var point = f.Em.GetComponentData<LocalTransform>(target).TransformPoint(new float3(.25f,.25f,0));
                    f.Fire(roots[i],point,target);
                }
                ulong before = CellStateHasher.ComputeWorldHash(f.Em); uint start = f.World.TicksRun;
                int frames = 0;
                while (f.World.TicksRun - start < 600)
                { f.World.StepFrame(1f / fps); Assert.Less(++frames,fps * 11); }
                Assert.AreEqual(600u,f.World.TicksRun - start);
                var stats = f.Store.Stats.Value; ulong hash = CellStateHasher.ComputeWorldHash(f.Em);
                Assert.Greater(stats.Fired,0); Assert.Greater(stats.Hits,0); Assert.AreEqual(0,stats.DroppedFull);
                Assert.AreNotEqual(before,hash,"The comparison must include actual weapon-caused cell damage.");
                TestContext.WriteLine($"WPN-012: fps={fps}, workers={workers}, ticks=600, hash={hash:X16}, fired={stats.Fired}, hits={stats.Hits}, live={stats.Live}, dropped={stats.DroppedFull}");
                return (hash,stats);
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }
        [Test] public void Skirmish_IdenticalHash()
        {
            var expected = Run(60,1);
            foreach (int workers in new[] { 1,JobsUtility.JobWorkerMaximumCount })
                foreach (int fps in new[] { 30,60,144 })
                {
                    if (workers == 1 && fps == 60) continue;
                    var actual = Run(fps,workers);
                    Assert.AreEqual(expected.hash,actual.hash,$"fps={fps}, workers={workers}");
                    Assert.AreEqual(expected.stats,actual.stats,"All ProjectileStoreStats fields must match.");
                }
        }
    }
}
