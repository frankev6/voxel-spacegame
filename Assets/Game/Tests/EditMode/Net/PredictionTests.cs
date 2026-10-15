using Game.Net.Replication;
using Game.Simulation.Commands;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Net
{
    /// <summary>SPEC-90 NET-011: the host's per-tick command queue and the guest's own-ship correction.</summary>
    [Category("Net")]
    public class PredictionTests
    {
        static PlayerCommand Thrust(float x) => new PlayerCommand { Thrust = new float2(0, x) };

        [Test]
        public void Queue_PlaysOneCommandPerTick_AndRepeatsWhenStarved()
        {
            var q = new PilotCommandQueue();
            for (uint s = 1; s <= 3; s++) q.Push(s, Thrust(s));
            q.Push(2, Thrust(99));   // redundant copy from a later packet: ignored
            for (uint s = 1; s <= 3; s++) { q.Pop(); Assert.AreEqual(s, q.Applied); Assert.AreEqual((float)s, q.Current.Thrust.y); }
            q.Pop();
            Assert.AreEqual(3u, q.Applied, "an empty queue repeats the last command without advancing the ack");
            Assert.AreEqual(1, q.Starved);
        }

        [Test]
        public void Queue_SkipsAheadWhenItBacksUp()
        {
            var q = new PilotCommandQueue();
            for (uint s = 1; s <= 20; s++) q.Push(s, Thrust(s));
            q.Pop();
            Assert.AreEqual(19u, q.Applied); Assert.AreEqual(1, q.Depth);
            Assert.AreEqual(18, q.Skipped);
        }

        [Test]
        public void Queue_TrimsStandingSurplusAfterAQuietSecond()
        {
            var q = new PilotCommandQueue();
            uint next = 1;
            for (int i = 0; i < 4; i++) q.Push(next++, default);
            for (int t = 0; t < PilotCommandQueue.WindowTicks; t++) { q.Push(next++, default); q.Pop(); }
            Assert.LessOrEqual(q.Depth, 1, "a buffer that never drained is trimmed to one tick");
            Assert.AreEqual(next - 1, q.Applied + (uint)q.Depth);
        }

        [Test]
        public void Queue_StarvingDeepensTheBuffer()
        {
            var q = new PilotCommandQueue();
            q.Push(1, default); q.Pop(); int before = q.Target;
            q.Pop();
            Assert.AreEqual(1, q.Starved); Assert.AreEqual(before + 1, q.Target);
            q.Push(2, default); q.Push(3, default); q.Push(4, default);
            for (int t = 0; t < PilotCommandQueue.WindowTicks - 3; t++) q.Pop();
            Assert.AreEqual(before + 1, q.Target, "a window with a starve keeps the deeper buffer");
        }

        static OwnShipPredictor.BodyState State(uint seq, double z, float v = 10) =>
            new OwnShipPredictor.BodyState { Sequence = seq, Position = new double3(0, 0, z), Rotation = quaternion.identity, Linear = new float3(0, 0, v) };

        [Test]
        public void Predictor_CorrectsPartOfTheError_AndShiftsNewerRecords()
        {
            var p = new OwnShipPredictor();
            for (uint s = 1; s <= 10; s++) p.Record(State(s, s));
            var current = State(10, 10);
            // The host has the ship 1 m further at seq 5.
            Assert.IsTrue(p.Reconcile(5, State(5, 6), ref current));
            Assert.AreEqual(1, p.LastErrorM, 1e-5); Assert.AreEqual(10 + OwnShipPredictor.PositionBlend, current.Position.z, 1e-5);
            // Seq 6 on the host is still 1 m ahead; the shifted record leaves only the remainder.
            Assert.IsTrue(p.Reconcile(6, State(6, 7), ref current));
            Assert.AreEqual(1 - OwnShipPredictor.PositionBlend, p.LastErrorM, 1e-5);
            Assert.IsFalse(p.Reconcile(6, State(6, 7), ref current), "an ack is applied once");
            Assert.AreEqual(0, p.Snaps);
        }

        [Test]
        public void Predictor_SnapsOnLargeErrors()
        {
            var p = new OwnShipPredictor();
            for (uint s = 1; s <= 4; s++) p.Record(State(s, s));
            var current = State(4, 4);
            Assert.IsTrue(p.Reconcile(2, State(2, 102), ref current));
            Assert.AreEqual(104, current.Position.z, 1e-5); Assert.AreEqual(1, p.Snaps);
        }
    }
}
