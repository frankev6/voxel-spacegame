using Game.Core.Systems;
using Game.App.Systems;
using Game.Simulation.Commands;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Fixture = Game.Tests.PlayMode.Simulation.Flight.FlightScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Flight
{
    internal struct FlightScript : IComponentData { public int Tick, Ship; }
    internal struct FlightMotionRecord : IBufferElementData
    {
        public int Tick;
        public LocalTransform Transform;
        public PhysicsVelocity Velocity;
    }
    [DisableAutoCreation, UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(PlayerCommandApplySystem))]
    internal partial struct FlightScriptSystem : ISystem
    {
        public void OnUpdate(ref SystemState state) => state.Dependency = new ScriptJob().ScheduleParallel(state.Dependency);
        [BurstCompile(CompileSynchronously = true)] partial struct ScriptJob : IJobEntity
        {
            void Execute(ref FlightScript script, ref ShipCommand command)
            {
                if (script.Tick >= 600) return;
                int t = script.Tick++, segment = t / 100;
                command = new ShipCommand
                {
                    Thrust = new float2(((t / 23 + script.Ship) % 3 - 1) * .4f, segment == 2 ? -.5f : segment == 5 ? 0 : .75f),
                    Turn = ((t / 37 + script.Ship) % 3 - 1) * .7f,
                    Flags = segment == 3 ? ShipCommandFlags.Boost : segment == 4 ? ShipCommandFlags.Brake : ShipCommandFlags.None
                };
            }
        }
    }
    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderLast = true), UpdateBefore(typeof(SimClockAdvanceSystem))]
    internal partial struct FlightRecordSystem : ISystem
    {
        public void OnUpdate(ref SystemState state) => state.Dependency = new RecordJob().ScheduleParallel(state.Dependency);
        [BurstCompile(CompileSynchronously = true)] partial struct RecordJob : IJobEntity
        {
            void Execute(in FlightScript script, in LocalTransform transform, in PhysicsVelocity velocity, ref DynamicBuffer<FlightMotionRecord> records)
            {
                if (records.Length >= 600) return;
                records.Add(new FlightMotionRecord { Tick = script.Tick - 1, Transform = transform, Velocity = velocity });
            }
        }
    }
    public sealed class FlightDeterminismTests
    {
        static FlightMotionRecord[][] Run(int pattern, int workers)
        {
            int previous = JobsUtility.JobWorkerCount; JobsUtility.JobWorkerCount = workers;
            try
            {
                using var f = new Fixture(typeof(FlightScriptSystem), typeof(FlightRecordSystem));
                var roots = new Entity[3];
                for (int i = 0; i < roots.Length; i++) roots[i] = f.Spawn(position: new float3(i * 1000, 0, 0));
                f.Ready(roots);
                foreach (var root in roots)
                    Assert.AreEqual(default(PhysicsVelocity), f.Velocity(root), "Preparation leaves a common resting state");
                for (int i = 0; i < roots.Length; i++)
                {
                    f.Em.AddComponentData(roots[i], new FlightScript { Ship = i });
                    var records = f.Em.AddBuffer<FlightMotionRecord>(roots[i]); records.Capacity = 600;
                }
                int tick = 0, frame = 0;
                while (tick < 600)
                {
                    int count = pattern switch { 0 => 1, 1 => 2, 2 => frame % 2, _ => 6 };
                    f.Test.StepFrame(count * Game.Tests.Common.EcsTestWorld.TickSeconds); tick += count; frame++;
                    Assert.LessOrEqual(frame, 1200);
                }
                var result = new FlightMotionRecord[3][];
                for (int i = 0; i < 3; i++)
                {
                    result[i] = f.Em.GetBuffer<FlightMotionRecord>(roots[i]).AsNativeArray().ToArray();
                    Assert.AreEqual(600, result[i].Length);
                    Assert.Greater(math.distance(result[i][0].Transform.Position, result[i][599].Transform.Position), 1);
                    float maxYaw = 0;
                    foreach (var record in result[i]) maxYaw = math.max(maxYaw, math.abs(record.Velocity.Angular.y));
                    Assert.Greater(maxYaw, .01f);
                }
                TestContext.WriteLine($"Flight determinism: {workers} workers, pattern {pattern}, {frame} frames, 1800 tick records");
                return result;
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }
        static uint[] Bits(FlightMotionRecord record)
        {
            var t = record.Transform; var v = record.Velocity;
            return new[] { math.asuint(t.Position.x), math.asuint(t.Position.y), math.asuint(t.Position.z),
                math.asuint(t.Rotation.value.x), math.asuint(t.Rotation.value.y), math.asuint(t.Rotation.value.z), math.asuint(t.Rotation.value.w), math.asuint(t.Scale),
                math.asuint(v.Linear.x), math.asuint(v.Linear.y), math.asuint(v.Linear.z), math.asuint(v.Angular.x), math.asuint(v.Angular.y), math.asuint(v.Angular.z) };
        }
        [Test]
        public void ScriptedFlight_IdenticalAcrossFramePatterns()
        {
            var expected = Run(0, 1);
            foreach (int workers in new[] { 1, JobsUtility.JobWorkerMaximumCount }) for (int pattern = 0; pattern < 4; pattern++)
            {
                if (workers == 1 && pattern == 0) continue;
                var actual = Run(pattern, workers);
                for (int ship = 0; ship < 3; ship++) for (int tick = 0; tick < 600; tick++)
                {
                    Assert.AreEqual(tick, actual[ship][tick].Tick);
                    CollectionAssert.AreEqual(Bits(expected[ship][tick]), Bits(actual[ship][tick]), $"Ship {ship}, tick {tick}, pattern {pattern}, workers {workers}");
                }
            }
        }
    }
}
