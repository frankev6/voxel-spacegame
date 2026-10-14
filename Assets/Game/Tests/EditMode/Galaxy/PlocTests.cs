using System;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class PlocTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() => EventHub.Reset();
        static byte[] Capture(PlayerLocationSaveParticipant participant)
        { using var writer = new SaveWriter(); participant.Capture(writer); return writer.ToArray(); }
        static GalaxyState State() => new GalaxyState
        {
            CurrentStarSystem = new StarSystemId(7), HasPlayerLocation = true,
            PlayerLocation = new PlayerLocation { StarSystem = new StarSystemId(7),
                PositionM = new double3(1234567890123.125, BitConverter.Int64BitsToDouble(long.MinValue), -0.000000000123), YawRad = 1.234567f }
        };
        [Test] public void RoundTrip_ByteIdentical()
        {
            using var state = State(); var docking = new DockingService();
            var participant = new PlayerLocationSaveParticipant(state, docking); byte[] expected = Capture(participant);
            state.Clear(); participant.Restore(new SaveReader(expected), 1);
            CollectionAssert.AreEqual(expected, Capture(participant));
            Assert.AreEqual(long.MinValue, BitConverter.DoubleToInt64Bits(state.PlayerLocation.PositionM.y));
            for (int size = 0; size < expected.Length; size++)
            {
                int truncatedSize = size;
                Assert.Throws<SaveFormatException>(() => participant.Restore(new SaveReader(expected, 0, truncatedSize), 1));
                CollectionAssert.AreEqual(expected, Capture(participant), "Truncation cannot partially replace live position");
            }
        }
        [Test] public void MissingChunk_Defaults()
        {
            using var state = State(); var docking = new DockingService();
            docking.RestoreDocked(new BodyId(state.CurrentStarSystem, BodyRole.Station, 1, 0));
            new PlayerLocationSaveParticipant(state, docking).Restore(SaveReader.Empty, 0);
            Assert.IsFalse(state.HasPlayerLocation); Assert.IsFalse(state.CurrentStarSystem.IsValid);
            Assert.AreEqual(default(PlayerLocation), state.PlayerLocation);
            Assert.IsFalse(docking.IsDocked); Assert.IsFalse(docking.HasPendingRestore);
        }
    }
}
