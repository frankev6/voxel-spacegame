using System;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Core.Events;
using Game.Galaxy.Travel;
using Game.Simulation.Commands;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>TRV-001: SPEC-32 §4 contract layout and Core-only event payloads.</summary>
    public sealed class TravelContractTests
    {
        [Test]
        public void Lock_IsUnmanaged()
        {
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<PlayerControlLock>());
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(typeof(PlayerControlLock)));
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(PlayerControlLockFlags)));
            Assert.AreEqual(1, UnsafeUtility.SizeOf<PlayerControlLock>());
            Assert.AreEqual(PlayerControlLockFlags.None, default(PlayerControlLock).Flags);
            Assert.AreEqual(0, (byte)PlayerControlLockFlags.None);
            Assert.AreEqual(1, (byte)PlayerControlLockFlags.Movement);
            Assert.AreEqual(2, (byte)PlayerControlLockFlags.Actions);
            Assert.AreEqual(3, (byte)(PlayerControlLockFlags.Movement | PlayerControlLockFlags.Actions));
            var fields = typeof(PlayerControlLock).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(1, fields.Length); Assert.AreEqual("Flags", fields[0].Name);
            Assert.AreEqual(typeof(PlayerControlLockFlags), fields[0].FieldType);
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<ArrivalPlan>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<ArrivalParams>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<NextHop>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<TravelStats>());
            Assert.IsFalse(UnsafeUtility.IsUnmanaged<GateInfo>());
        }

        [Test]
        public void Events_InCoreNamespace()
        {
            var assembly = typeof(ContentHash).Assembly;
            foreach (var type in new[] { typeof(JumpSource), typeof(LaneJumped), typeof(TravelNoticeKind), typeof(TravelNotice) })
            {
                Assert.AreEqual("Game.Core.Events", type.Namespace);
                Assert.AreEqual(assembly, type.Assembly);
            }
            foreach (var type in new[] { typeof(LaneJumped), typeof(TravelNotice) })
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.IsTrue(field.IsInitOnly, type.Name + "." + field.Name);
                    Assert.IsTrue(field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.Assembly == assembly,
                        type.Name + "." + field.Name + " must remain Core-visible");
                }
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<LaneJumped>());
            Assert.IsFalse(UnsafeUtility.IsUnmanaged<TravelNotice>());
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(JumpSource)));
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, Enum.GetValues(typeof(JumpSource)).Cast<JumpSource>().Select(x => (byte)x).ToArray());
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(TravelNoticeKind)));
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, Enum.GetValues(typeof(TravelNoticeKind)).Cast<TravelNoticeKind>().Select(x => (byte)x).ToArray());
            var jumped = new LaneJumped(12, 34, -1, JumpSource.Narrative);
            Assert.AreEqual(12u, jumped.FromStarSystemValue); Assert.AreEqual(34u, jumped.ToStarSystemValue);
            Assert.AreEqual(-1, jumped.LaneIndex); Assert.AreEqual(JumpSource.Narrative, jumped.Source);
            var notice = new TravelNotice(TravelNoticeKind.JumpRefused, 34, null);
            Assert.AreEqual(TravelNoticeKind.JumpRefused, notice.Kind); Assert.AreEqual(34u, notice.StarSystemValue); Assert.AreEqual("", notice.Text);
            Assert.AreEqual("Route restored", new TravelNotice(TravelNoticeKind.RouteRestored, 34, "Route restored").Text);
        }
    }
}
