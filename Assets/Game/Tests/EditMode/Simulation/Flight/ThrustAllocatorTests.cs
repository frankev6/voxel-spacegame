using Game.Simulation.Flight;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-004: bounded allocation, per-coordinate descent and managed/Burst bit parity (SPEC-20 §5.4).</summary>
    public sealed class ThrustAllocatorTests
    {
        static ThrustActuator Actuator(float3 column, float min = 0, float max = 1)
            => new ThrustActuator { Column = column, Min = min, Max = max };
        static double Residual(in ThrustProblem problem, NativeArray<ThrustActuator> a, NativeArray<float> u)
        {
            double3 residual = -(double3)problem.Target;
            for (int k = 0; k < a.Length; k++) residual += (double3)a[k].Column * u[k];
            residual *= (double3)problem.Weights;
            return math.dot(residual, residual);
        }

        [Test]
        public void Residual_NonIncreasing()
        {
            var random = new Random(5417);
            using var aStorage = new NativeArray<ThrustActuator>(20, Allocator.Temp);
            var a = aStorage;
            using var uStorage = new NativeArray<float>(20, Allocator.Temp);
            var u = uStorage;
            for (int scenario = 0; scenario < 100; scenario++)
            {
                var p = new ThrustProblem { Target = random.NextFloat3(-20, 20), Weights = random.NextFloat3(.1f, 3), Sweeps = 6 };
                for (int k = 0; k < a.Length; k++) { a[k] = Actuator(random.NextFloat3(-10, 10), -1); u[k] = random.NextFloat(-1, 1); }
                float3 residual = ThrustAllocator.Initialize(p, a, u);
                double before = Residual(p, a, u);
                for (int sweep = 0; sweep < p.Sweeps; sweep++)
                    for (int k = 0; k < a.Length; k++)
                    {
                        u[k] = ThrustAllocator.Update(a[k], p.Weights, u[k], ref residual);
                        double after = Residual(p, a, u);
                        Assert.LessOrEqual(after, before + 1e-5 * math.max(1, before), "one coordinate update, independently recomputed in double precision");
                        before = after;
                    }
            }
        }

        [Test]
        public void Bounds_Respected()
        {
            var random = new Random(817);
            using var aStorage = new NativeArray<ThrustActuator>(40, Allocator.Temp);
            var a = aStorage;
            using var uStorage = new NativeArray<float>(40, Allocator.Temp);
            var u = uStorage;
            for (int scenario = 0; scenario < 100; scenario++)
            {
                var p = new ThrustProblem { Target = random.NextFloat3(-1000, 1000), Weights = new float3(1, .5f, 2), Sweeps = 6 };
                for (int k = 0; k < a.Length; k++)
                {
                    bool unavailable = k % 4 == 0;
                    a[k] = Actuator(k % 3 == 0 ? float3.zero : random.NextFloat3(-20, 20), unavailable ? 0 : -.5f, unavailable ? 0 : 1.25f);
                    u[k] = random.NextFloat(-5, 5); // Warm starts can be stale after bounds change.
                }
                Assert.IsTrue(math.isfinite(ThrustAllocator.Solve(p, a, u)));
                for (int k = 0; k < a.Length; k++) Assert.That(u[k], Is.InRange(a[k].Min, a[k].Max));
            }
        }

        [Test]
        public void Symmetric_EqualThrottles()
        {
            using var aStorage = new NativeArray<ThrustActuator>(new[] {
                Actuator(new float3(0, 1, -1)), Actuator(new float3(0, 1, 1)), Actuator(new float3(0, 0, 1), -1)
            }, Allocator.Temp);
            var a = aStorage;
            using var uStorage = new NativeArray<float>(3, Allocator.Temp);
            var u = uStorage;
            var p = new ThrustProblem { Target = new float3(0, 1, 0), Weights = new float3(1), Sweeps = 6 };
            Assert.AreEqual(0, ThrustAllocator.Solve(p, a, u));
            Assert.AreEqual(.5f, u[0]); Assert.AreEqual(.5f, u[1]); Assert.AreEqual(0, u[2]);
            // Q10: a redundant gyro and different row weights do not impose a unique symmetric optimum.
            p.Weights.z = 2; u[0] = 0; u[1] = 0; u[2] = 0;
            Assert.Less(ThrustAllocator.Solve(p, a, u), .00015f);
            Assert.AreEqual(.3799113f, u[0], .000002f); Assert.AreEqual(.6078581f, u[1], .000002f);
            Assert.AreEqual(-.2279468f, u[2], .000002f);
        }

        [Test]
        public void OffCentre_NoGyro_LeavesTorque()
        {
            using var aStorage = new NativeArray<ThrustActuator>(new[] {
                Actuator(new float3(0, 10, -30)), Actuator(float3.zero, -1)
            }, Allocator.Temp);
            var a = aStorage;
            using var uStorage = new NativeArray<float>(2, Allocator.Temp);
            var u = uStorage;
            var p = new ThrustProblem { Target = new float3(0, 10, 0), Weights = new float3(1), Sweeps = 6 };
            Assert.AreEqual(90, ThrustAllocator.Solve(p, a, u), .00001);
            Assert.AreEqual(.1f, u[0], .000001); Assert.AreEqual(0, u[1]);
            Assert.AreEqual(-3, a[0].Column.z * u[0], .00001, "force 3 m right of centre retains negative yaw torque from cross(r, F)");
        }

        [Test]
        public void Saturation_ScalesDown()
        {
            using var aStorage = new NativeArray<ThrustActuator>(new[] { Actuator(new float3(0, 10, 0)) }, Allocator.Temp);
            var a = aStorage;
            using var uStorage = new NativeArray<float>(1, Allocator.Temp);
            var u = uStorage;
            var p = new ThrustProblem { Target = new float3(0, 100, 0), Weights = new float3(1), Sweeps = 6 };
            Assert.AreEqual(8100, ThrustAllocator.Solve(p, a, u)); Assert.AreEqual(1, u[0]);
            p.Target.y = 5;
            Assert.AreEqual(0, ThrustAllocator.Solve(p, a, u)); Assert.AreEqual(.5f, u[0]);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
        struct SolveJob : IJob
        {
            public ThrustProblem Problem;
            [ReadOnly] public NativeArray<ThrustActuator> Actuators;
            public NativeArray<float> U, Result;
            public void Execute() => Result[0] = ThrustAllocator.Solve(Problem, Actuators, U);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
        struct TraceJob : IJob
        {
            public ThrustProblem Problem;
            [ReadOnly] public NativeArray<ThrustActuator> Actuators;
            public NativeArray<float> U;
            public NativeArray<float4> Trace;
            public void Execute()
            {
                float3 residual = ThrustAllocator.Initialize(Problem, Actuators, U);
                Trace[0] = new float4(residual, 0);
                int at = 1;
                for (int sweep = 0; sweep < Problem.Sweeps; sweep++)
                    for (int k = 0; k < Actuators.Length; k++)
                    {
                        U[k] = ThrustAllocator.Update(Actuators[k], Problem.Weights, U[k], ref residual);
                        Trace[at++] = new float4(residual, U[k]);
                    }
            }
        }

        [Test]
        public void Deterministic()
        {
            Assert.IsTrue(BurstCompiler.Options.EnableBurstCompilation, "This comparison must execute the compiled Burst job");
            using var aStorage = new NativeArray<ThrustActuator>(21, Allocator.TempJob);
            var a = aStorage;
            using var managedStorage = new NativeArray<float>(21, Allocator.TempJob);
            var managed = managedStorage;
            using var burstStorage = new NativeArray<float>(21, Allocator.TempJob);
            var burst = burstStorage;
            using var repeatedStorage = new NativeArray<float>(21, Allocator.TempJob);
            var repeated = repeatedStorage;
            using var resultStorage = new NativeArray<float>(1, Allocator.TempJob);
            var result = resultStorage;
            using var traceManagedStorage = new NativeArray<float4>(127, Allocator.TempJob);
            using var traceBurstStorage = new NativeArray<float4>(127, Allocator.TempJob);
            using var traceUStorage = new NativeArray<float>(21, Allocator.TempJob);
            var random = new Random(711);
            for (int scenario = 0; scenario < 100; scenario++)
            {
                var p = new ThrustProblem { Target = random.NextFloat3(-100, 100), Weights = random.NextFloat3(.1f, 3), Sweeps = 6 };
                for (int k = 0; k < a.Length; k++)
                {
                    a[k] = Actuator(random.NextFloat3(-10, 10), k == 20 ? -1 : 0);
                    managed[k] = burst[k] = repeated[k] = random.NextFloat(a[k].Min, a[k].Max);
                }
                NativeArray<float>.Copy(managed, traceUStorage);
                new TraceJob { Problem = p, Actuators = a, U = traceUStorage, Trace = traceManagedStorage }.Execute();
                NativeArray<float>.Copy(managed, traceUStorage);
                new TraceJob { Problem = p, Actuators = a, U = traceUStorage, Trace = traceBurstStorage }.Schedule().Complete();
                for (int step = 0; step < traceManagedStorage.Length; step++)
                    Assert.AreEqual(math.asuint(traceManagedStorage[step]), math.asuint(traceBurstStorage[step]), "trace scenario " + scenario + ", step " + step);
                float expected = ThrustAllocator.Solve(p, a, managed);
                new SolveJob { Problem = p, Actuators = a, U = burst, Result = result }.Schedule().Complete();
                float firstResult = result[0];
                new SolveJob { Problem = p, Actuators = a, U = repeated, Result = result }.Schedule().Complete();
                for (int k = 0; k < a.Length; k++)
                {
                    Assert.AreEqual(math.asuint(managed[k]), math.asuint(burst[k]), "scenario " + scenario + ", actuator " + k);
                    Assert.AreEqual(math.asuint(managed[k]), math.asuint(repeated[k]));
                }
                Assert.AreEqual(math.asuint(expected), math.asuint(firstResult), "residual, scenario " + scenario);
                Assert.AreEqual(math.asuint(expected), math.asuint(result[0]));
            }
        }
    }
}
