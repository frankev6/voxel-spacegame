using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Components;
using Game.Core.Systems;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-008: group placement and pipeline order (SPEC-02 §5.8, architecture §5).</summary>
    public sealed partial class SystemGroupOrderTests
    {
        // Test-only systems placed in the two gameplay groups.
        public struct TestCounter : IComponentData { public int Pre, Post; }
        public struct EcbMarker : IComponentData { }

        [DisableAutoCreation, UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
        public partial class TestPreSystem : SystemBase
        {
            public int Updates; public bool RecordEcb;
            protected override void OnUpdate()
            {
                Updates++;
                if (!RecordEcb) return;
                var ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                ecb.AddComponent<EcbMarker>(ecb.CreateEntity());
            }
        }

        [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
        public partial class TestPostSystem : SystemBase
        {
            public int Updates; public int MarkersSeenDuringPost;
            protected override void OnUpdate()
            {
                Updates++;
                MarkersSeenDuringPost += SystemAPI.QueryBuilder().WithAll<EcbMarker>().Build().CalculateEntityCount();
            }
        }

        static World fullWorld;

        static World FullWorld()
        {
            if (fullWorld != null && fullWorld.IsCreated) return fullWorld;
            fullWorld = new World("order");
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(fullWorld, DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));
            foreach (var root in new ComponentSystemGroup[] { fullWorld.GetExistingSystemManaged<InitializationSystemGroup>(),
                                                              fullWorld.GetExistingSystemManaged<SimulationSystemGroup>(),
                                                              fullWorld.GetExistingSystemManaged<PresentationSystemGroup>() })
                root.SortSystems();
            return fullWorld;
        }

        [OneTimeTearDown] public void OneTimeTearDown() { if (fullWorld != null && fullWorld.IsCreated) fullWorld.Dispose(); fullWorld = null; }

        static List<Type> Order<TGroup>(World w) where TGroup : ComponentSystemGroup
        {
            using var handles = w.GetExistingSystemManaged<TGroup>().GetAllSystems(Allocator.Temp);
            var types = new List<Type>();
            foreach (var h in handles) types.Add(TypeManager.GetSystemType(w.Unmanaged.GetSystemTypeIndex(h)));
            return types;
        }

        static void AssertBefore(List<Type> order, Type a, Type b)
        {
            int ia = order.IndexOf(a), ib = order.IndexOf(b);
            Assert.GreaterOrEqual(ia, 0, $"{a.Name} missing"); Assert.GreaterOrEqual(ib, 0, $"{b.Name} missing");
            Assert.Less(ia, ib, $"{a.Name} must run before {b.Name}: {string.Join(", ", order.Select(t => t.Name))}");
        }

        [Test]
        public void FixedStep_Order()
        {
            var o = Order<FixedStepSimulationSystemGroup>(FullWorld());
            AssertBefore(o, typeof(BeginFixedStepSimulationEntityCommandBufferSystem), typeof(GameplayPrePhysicsGroup));
            AssertBefore(o, typeof(GameplayPrePhysicsGroup), typeof(PhysicsSystemGroup));
            AssertBefore(o, typeof(PhysicsSystemGroup), typeof(GameplayPostPhysicsGroup));
            AssertBefore(o, typeof(GameplayPostPhysicsGroup), typeof(EndFixedStepSimulationEntityCommandBufferSystem));
        }

        [Test]
        public void PrePhysics_ShipCommandGroupFirst()
        {
            var o = Order<GameplayPrePhysicsGroup>(FullWorld());
            Assert.AreEqual(typeof(ShipCommandGroup), o[0], string.Join(", ", o.Select(t => t.Name)));
        }

        [Test]
        public void PostPhysics_EndEcbLastBucket()
        {
            var o = Order<GameplayPostPhysicsGroup>(FullWorld());
            int ecb = o.IndexOf(typeof(GameplayEndCommandBufferSystem));
            Assert.GreaterOrEqual(ecb, 0);
            // Only SPEC-01's SimClockAdvanceSystem may follow it (OrderLast with UpdateAfter the ECB system).
            Assert.IsTrue(o.Skip(ecb + 1).All(t => t.Name == "SimClockAdvanceSystem"), string.Join(", ", o.Select(t => t.Name)));
        }

        [Test]
        public void Simulation_VariableRateOrder()
        {
            var o = Order<SimulationSystemGroup>(FullWorld());
            AssertBefore(o, typeof(FixedStepSimulationSystemGroup), typeof(VoxelRebuildGroup));
            AssertBefore(o, typeof(VoxelRebuildGroup), typeof(WorldGroup));
            AssertBefore(o, typeof(WorldGroup), typeof(TransformSystemGroup));
        }

        [Test]
        public void Presentation_BridgeBeforeGraphics()
        {
            var o = Order<PresentationSystemGroup>(FullWorld());
            int bridge = o.IndexOf(typeof(PresentationBridgeGroup));
            Assert.GreaterOrEqual(bridge, 0);
            AssertBefore(o, typeof(BeginPresentationEntityCommandBufferSystem), typeof(PresentationBridgeGroup));
            var graphics = o.Where(t => t.Namespace != null && t.Namespace.StartsWith("Unity.Rendering", StringComparison.Ordinal)).ToList();
            Assert.Greater(graphics.Count, 0, "Entities Graphics systems are present");
            foreach (var g in graphics) AssertBefore(o, typeof(PresentationBridgeGroup), g);
        }

        [Test]
        public void Init_GameInitGroupLast()
        {
            var o = Order<InitializationSystemGroup>(FullWorld());
            int gi = o.IndexOf(typeof(GameInitGroup)), end = o.IndexOf(typeof(EndInitializationEntityCommandBufferSystem));
            Assert.Less(gi, end);
            Assert.IsTrue(o.Skip(gi + 1).All(t => t == typeof(EndInitializationEntityCommandBufferSystem) ||
                                                 o.IndexOf(t) > end), $"only the end ECB (and systems after it) follow GameInitGroup: {string.Join(", ", o.Select(t => t.Name))}");
        }

        [Test]
        public void NoOrderingWarnings()
        {
            var invalid = new List<string>();
            void Listen(string message, string stack, UnityEngine.LogType type) { if (message.Contains("Ignoring invalid")) invalid.Add(message); }
            UnityEngine.Application.logMessageReceived += Listen;
            try
            {
                if (fullWorld != null && fullWorld.IsCreated) { fullWorld.Dispose(); fullWorld = null; }
                FullWorld();
            }
            finally { UnityEngine.Application.logMessageReceived -= Listen; }
            CollectionAssert.IsEmpty(invalid, "Entities ordering warnings during world creation");
        }

        static (World w, FixedStepSimulationSystemGroup fixedStep, TestPreSystem pre, TestPostSystem post) GameplayWorld()
        {
            var w = new World("gameplay");
            var fixedStep = w.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var preGroup = w.CreateSystemManaged<GameplayPrePhysicsGroup>();
            var postGroup = w.CreateSystemManaged<GameplayPostPhysicsGroup>();
            var pre = w.CreateSystemManaged<TestPreSystem>();
            var post = w.CreateSystemManaged<TestPostSystem>();
            var ecb = w.CreateSystemManaged<GameplayEndCommandBufferSystem>();
            var physics = w.CreateSystemManaged<PhysicsSystemGroup>();   // the anchor of the pre/post UpdateBefore/After attributes
            fixedStep.AddSystemToUpdateList(preGroup); fixedStep.AddSystemToUpdateList(physics); fixedStep.AddSystemToUpdateList(postGroup);
            preGroup.AddSystemToUpdateList(pre);
            postGroup.AddSystemToUpdateList(post); postGroup.AddSystemToUpdateList(ecb);
            fixedStep.SortSystems();
            fixedStep.RateManager = null;             // one Update() = one fixed tick
            return (w, fixedStep, pre, post);
        }

        [Test]
        public void GameplayGroups_RequireGameplayActive()
        {
            var (w, fixedStep, pre, post) = GameplayWorld();
            try
            {
                fixedStep.Update(); fixedStep.Update();
                Assert.AreEqual(0, pre.Updates); Assert.AreEqual(0, post.Updates);
                var active = w.EntityManager.CreateEntity(typeof(GameplayActive));
                fixedStep.Update(); fixedStep.Update(); fixedStep.Update();
                Assert.AreEqual(3, pre.Updates, "once per fixed tick"); Assert.AreEqual(3, post.Updates);
                w.EntityManager.DestroyEntity(active);
                fixedStep.Update();
                Assert.AreEqual(3, pre.Updates); Assert.AreEqual(3, post.Updates);
            }
            finally { w.Dispose(); }
        }

        [Test]
        public void EndEcb_PlaysBackSameTick()
        {
            var (w, fixedStep, pre, post) = GameplayWorld();
            try
            {
                w.EntityManager.CreateEntity(typeof(GameplayActive));
                Assert.AreEqual(1, new EntityQueryBuilder(Allocator.Temp).WithAll<GameplayEndCommandBufferSystem.Singleton>().WithOptions(EntityQueryOptions.IncludeSystems).Build(w.EntityManager).CalculateEntityCount(), "ECB singleton exists (on the system entity)");
                pre.RecordEcb = true;
                fixedStep.Update();
                Assert.AreEqual(1, pre.Updates, "pre ran"); Assert.AreEqual(1, post.Updates, "post ran");
                Assert.AreEqual(0, post.MarkersSeenDuringPost, "not visible before the ECB system plays back");
                Assert.AreEqual(1, w.EntityManager.CreateEntityQuery(typeof(EcbMarker)).CalculateEntityCount(), "played back in the same tick");
            }
            finally { w.Dispose(); }
        }
    }
}
