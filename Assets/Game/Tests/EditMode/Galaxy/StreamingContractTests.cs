using System;
using System.Reflection;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Galaxy.Services;
using Game.Galaxy.Streaming;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>SPEC-31 SYS-001: unmanaged ECS/streaming data and Core-only immutable event payloads.</summary>
    public sealed class StreamingContractTests
    {
        [Test] public void Components_AreUnmanaged()
        {
            var components = new[] { typeof(StarSystemBody), typeof(BodyOrbit), typeof(BodyGridOwner), typeof(BodyGridInitialCells),
                typeof(CurrentStarSystemInfo), typeof(StreamingConfig), typeof(Interactable), typeof(InteractionTarget), typeof(InteractionInputState) };
            foreach (var type in components)
            {
                Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(type), type.FullName);
                Assert.IsTrue(UnsafeUtility.IsUnmanaged(type), type.FullName);
            }
            foreach (var type in new[] { typeof(LoadedBody), typeof(BeltSector), typeof(AsteroidSlot), typeof(BodyGridEvent), typeof(PersistentIdValue) })
                Assert.IsTrue(UnsafeUtility.IsUnmanaged(type), type.FullName);
            Assert.IsFalse(typeof(IComponentData).IsAssignableFrom(typeof(GalaxyShipSpawnArgs)), "Managed ContentId belongs in the managed spawn seam, never ECS data.");
            Assert.IsFalse(UnsafeUtility.IsUnmanaged(typeof(GalaxyShipSpawnArgs)));
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(BodyTier)));
            Assert.AreEqual(1, (byte)BodyTier.T1); Assert.AreEqual(2, (byte)BodyTier.T2); Assert.AreEqual(3, (byte)BodyTier.T3);
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(BodyFlags)));
            Assert.AreEqual(0, (byte)BodyFlags.None); Assert.AreEqual(1, (byte)BodyFlags.HasGrid);
            Assert.AreEqual(2, (byte)BodyFlags.GridPending); Assert.AreEqual(4, (byte)BodyFlags.Destroyed);
            Assert.AreEqual(8, (byte)BodyFlags.HasCollider); Assert.AreEqual(16, (byte)BodyFlags.OnRails); Assert.AreEqual(32, (byte)BodyFlags.Asteroid);
            Assert.AreEqual(0, (byte)LoaderState.Idle); Assert.AreEqual(1, (byte)LoaderState.Unloading);
            Assert.AreEqual(2, (byte)LoaderState.Loading); Assert.AreEqual(3, (byte)LoaderState.Ready);
            // Named contracts expose the original identity types, not parallel ID/interaction enums.
            Assert.AreEqual(typeof(Game.Galaxy.Data.BodyId), typeof(StarSystemBody).GetField(nameof(StarSystemBody.Id)).FieldType);
            Assert.AreEqual(typeof(InteractionKind), typeof(Interactable).GetField(nameof(Interactable.Kind)).FieldType);
            Assert.AreEqual(typeof(Game.Simulation.Allegiance), typeof(GalaxyShipSpawnArgs).GetField(nameof(GalaxyShipSpawnArgs.Allegiance)).FieldType);
            Assert.IsTrue(typeof(Game.Core.Save.ISaveParticipant).IsAssignableFrom(typeof(PlayerLocationSaveParticipant)));
            Assert.IsTrue(typeof(IGalaxyFrameTick).IsAssignableFrom(typeof(StarSystemLoader)));
            Assert.IsTrue(typeof(IGalaxyLiveDeltaSink).IsAssignableFrom(typeof(StarSystemLoader)));
            Assert.IsTrue(typeof(Game.Core.Flow.ISceneReady).IsAssignableFrom(typeof(StarSystemLoader)));
            Assert.IsTrue(typeof(Game.Core.Flow.ISceneExiting).IsAssignableFrom(typeof(StarSystemLoader)));
        }
        [Test] public void Events_CoreOnly()
        {
            var core = typeof(StarSystemEntered).Assembly;
            foreach (var type in new[] { typeof(StarSystemEntered), typeof(BodyInteracted), typeof(BodyDestroyed), typeof(DockStateChanged) })
            {
                Assert.AreSame(core, type.Assembly, type.FullName);
                Assert.AreEqual("Game.Core.Events", type.Namespace);
                Assert.IsTrue(type.IsValueType); Assert.IsTrue(UnsafeUtility.IsUnmanaged(type), type.FullName);
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.IsTrue(field.IsInitOnly, type.Name + "." + field.Name);
                    Assert.IsTrue(field.FieldType.IsPrimitive || (field.FieldType.IsEnum && field.FieldType.Assembly == core), type.Name + "." + field.Name);
                }
            }
            Assert.AreSame(core, typeof(StarSystemEntryReason).Assembly);
            Assert.AreEqual(0, (byte)StarSystemEntryReason.NewGame); Assert.AreEqual(1, (byte)StarSystemEntryReason.Load);
            Assert.AreEqual(2, (byte)StarSystemEntryReason.Jump); Assert.AreEqual(3, (byte)StarSystemEntryReason.ReturnFromEditor);
            Assert.AreEqual(4, (byte)StarSystemEntryReason.DevSandbox);
            var entered = new StarSystemEntered(17, StarSystemEntryReason.Jump, 3);
            Assert.AreEqual(17u, entered.StarSystemValue); Assert.AreEqual(StarSystemEntryReason.Jump, entered.Reason); Assert.AreEqual(3u, entered.VisitCount);
            var interacted = new BodyInteracted(17, ulong.MaxValue, (byte)InteractionKind.Dock, 81);
            Assert.AreEqual(17u, interacted.StarSystemValue); Assert.AreEqual(ulong.MaxValue, interacted.BodyKey);
            Assert.AreEqual((byte)InteractionKind.Dock, interacted.Kind); Assert.AreEqual(81ul, interacted.GridPersistentId);
            var destroyed = new BodyDestroyed(17, ulong.MaxValue);
            Assert.AreEqual(17u, destroyed.StarSystemValue); Assert.AreEqual(ulong.MaxValue, destroyed.BodyKey);
            var docked = new DockStateChanged(true, 17, ulong.MaxValue);
            Assert.IsTrue(docked.Docked); Assert.AreEqual(17u, docked.StarSystemValue); Assert.AreEqual(ulong.MaxValue, docked.BodyKey);
        }
    }
}
