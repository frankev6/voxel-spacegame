using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Combat;
using Game.Presentation.Vfx;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
namespace Game.Tests.EditMode.Vfx
{
    public sealed class TracerGatherJobTests
    {
        static TracerGatherJob Job(NativeArray<ProjectileRow> rows, NativeArray<TracerInstance> output) => new TracerGatherJob {
            Rows = rows, Output = output, LengthSeconds = .02f, MinLength = .5f, MaxLength = 12, Width = .15f,
            Color0 = new float4(1,0,0,1), Color1 = new float4(0,1,0,1), Color2 = new float4(0,0,1,1), Color3 = new float4(1) };
        [Test] public void DeadRows_Skipped()
        {
            using var rowsOwner = new NativeArray<ProjectileRow>(2, Allocator.TempJob); var rows = rowsOwner; using var output = new NativeArray<TracerInstance>(2, Allocator.TempJob);
            rows[0] = new ProjectileRow { Flags = ProjectileFlags.Dead, Velocity = new float3(100,0,0) };
            Job(rows, output).Schedule(2, 1).Complete(); Assert.AreEqual(0, output[0].Width); Assert.AreEqual(0, output[1].Width);
        }
        [Test] public void OverCap_WritesEachIndexOnce()
        {
            using var rowsOwner = new NativeArray<ProjectileRow>(8192, Allocator.TempJob); var rows = rowsOwner; using var output = new NativeArray<TracerInstance>(4096, Allocator.TempJob);
            for (int i = 0; i < rows.Length; i++) rows[i] = new ProjectileRow { Position = new float3(i,0,0), Velocity = new float3(100,0,0) };
            Job(rows, output).Schedule(output.Length, 8).Complete(); for (int i = 0; i < output.Length; i++) Assert.AreEqual(i, output[i].Head.x);
        }
        [Test] public void PendingShift_Corrected()
        {
            Assert.IsTrue(TracerGatherJob.PendingShift(new OriginShiftRecord { Sequence = 3, Delta = new float3(-2000,0,0) }, 2, out var correction));
            using var rowsOwner = new NativeArray<ProjectileRow>(1, Allocator.TempJob); var rows = rowsOwner; using var output = new NativeArray<TracerInstance>(1, Allocator.TempJob);
            rows[0] = new ProjectileRow { Position = new float3(1990,0,0), Velocity = new float3(100,0,0) };
            var job = Job(rows, output); job.Correction = correction; job.Run(1); Assert.AreEqual(-10, output[0].Head.x);
        }
        [Test] public void HeavyShell_DrawsWiderAndLonger()
        {
            using var rowsOwner = new NativeArray<ProjectileRow>(2, Allocator.TempJob); var rows = rowsOwner; using var output = new NativeArray<TracerInstance>(2, Allocator.TempJob);
            using var scales = new NativeArray<float>(new[] { 1f, TracerGatherJob.Scale(.1f), TracerGatherJob.Scale(.35f) }, Allocator.TempJob);
            rows[0] = new ProjectileRow { Velocity = new float3(100,0,0), Weapon = 1 }; rows[1] = new ProjectileRow { Velocity = new float3(100,0,0), Weapon = 2 };
            var job = Job(rows, output); job.WeaponScale = scales; job.Run(2);
            Assert.AreEqual(.15f, output[0].Width, 1e-6f); Assert.AreEqual(2, output[0].Length, 1e-6f);
            Assert.AreEqual(.525f, output[1].Width, 1e-5f); Assert.AreEqual(7, output[1].Length, 1e-5f);
            Assert.AreEqual(TracerGatherJob.MaxScale, TracerGatherJob.Scale(5));
        }
        [Test] public void StarSystemChange_DrawsNothing() => Assert.IsFalse(TracerGatherJob.PendingShift(new OriginShiftRecord { Sequence = 3, StarSystemChanged = 1 }, 2, out _));
        [Test] public void LengthAndColour_FollowSpec()
        {
            using var rowsOwner = new NativeArray<ProjectileRow>(3, Allocator.TempJob); var rows = rowsOwner; using var output = new NativeArray<TracerInstance>(3, Allocator.TempJob);
            rows[0] = new ProjectileRow { Velocity = new float3(1,0,0) }; rows[1] = new ProjectileRow { Velocity = new float3(100,0,0), Type = (DamageType)1 };
            rows[2] = new ProjectileRow { Velocity = new float3(1000,0,0), Type = (DamageType)2 };
            Job(rows, output).Run(3); Assert.AreEqual(.5f, output[0].Length); Assert.AreEqual(2, output[1].Length); Assert.AreEqual(12, output[2].Length);
            Assert.AreEqual(new float4(0,1,0,1), output[1].Color); Assert.AreEqual(new float3(-1,0,0), output[2].Dir);
        }
    }
}
