using System;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Input;
using Game.Presentation.Components;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using BoxCollider = Unity.Physics.BoxCollider;
using Collider = Unity.Physics.Collider;

namespace Game.Tests.EditMode.Input
{
    /// <summary>INP-005 specified mapping, latching, pointer, collider and allocation checks using real Input System events.</summary>
    public sealed class PlayerInputSampleSystemTests : InputTestFixture
    {
        sealed class Gate : IWorldPointerGate
        { public bool Allowed = true; public bool IsWorldPointerAllowed(Vector2 point) => Allowed; }
        World world;
        InputService input;
        PlayerInputSampleSystem sampler;
        Keyboard keyboard;
        Mouse mouse;
        Gate gate;
        EntityQuery commands;
        PhysicsWorld physicsWorld;
        BlobAssetReference<Collider> collider;
        bool hasPhysics;
        [SetUp] public override void Setup()
        {
            base.Setup(); EventHub.Reset();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            input = new InputService(new ControlSettingsData()); input.SetContext(InputContext.Flight); gate = new Gate();
            world = new World("PlayerInputSampleSystemTests"); sampler = world.GetOrCreateSystemManaged<PlayerInputSampleSystem>();
            sampler.Bind(input, gate); commands = world.EntityManager.CreateEntityQuery(typeof(PlayerCommand));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(100, 100) }); InputSystem.Update();
        }
        [TearDown] public override void TearDown()
        {
            try
            {
                world?.Dispose(); if (hasPhysics) physicsWorld.Dispose(); if (collider.IsCreated) collider.Dispose();
                hasPhysics = false; collider = default; input?.Dispose(); EventHub.Reset(); LogAssert.NoUnexpectedReceived();
            }
            finally { base.TearDown(); }
        }
        PlayerCommand Command => commands.GetSingleton<PlayerCommand>();
        void Frame(params Key[] held)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); InputSystem.Update(); sampler.Update(); }
        void Camera()
        {
            var inverse = new float4x4(new float4(10, 0, 0, 0), new float4(0, 0, 10, 0), new float4(0, -100, 0, 0), new float4(20, 100, 30, 1));
            world.EntityManager.CreateSingleton(new MainCameraState { ScreenSizePx = new float2(200), ViewProjection = math.inverse(inverse) });
        }
        void Box(bool player)
        {
            var entity = world.EntityManager.CreateEntity(); if (player) world.EntityManager.AddComponent<PlayerShip>(entity);
            collider = BoxCollider.Create(new BoxGeometry { Center = float3.zero, Size = new float3(10), Orientation = quaternion.identity, BevelRadius = 0 }, PhysicsLayers.Grid);
            physicsWorld = new PhysicsWorld(1, 0, 0); hasPhysics = true;
            var bodies = physicsWorld.StaticBodies;
            bodies[0] = new RigidBody { Entity = entity, Collider = collider, WorldFromBody = new RigidTransform(quaternion.identity, new float3(20, 100, 30)), Scale = 1 };
            physicsWorld.CollisionWorld.BuildBroadphase(ref physicsWorld, 1f / 60, float3.zero);
            world.EntityManager.CreateSingleton(new PhysicsWorldSingleton { PhysicsWorld = physicsWorld });
        }
        void AssertAim(float3 expected)
        { Assert.AreEqual(1, Command.AimValid); Assert.Less(math.distance(expected, Command.AimWorld), .001f); }
        [Test] public void Analog_Mapping()
        {
            Frame(Key.W, Key.Q, Key.D, Key.LeftShift); Assert.AreEqual(new float2(-1, 1), Command.Thrust); Assert.AreEqual(1, Command.Turn);
            Assert.AreEqual(ShipCommandFlags.Boost, Command.Flags);
            Frame(Key.S, Key.E, Key.A, Key.X); Assert.AreEqual(new float2(1, -1), Command.Thrust); Assert.AreEqual(-1, Command.Turn);
            Assert.AreEqual(ShipCommandFlags.Brake, Command.Flags);
            input.SetContext(InputContext.Menu); Frame(Key.W); Assert.AreEqual(float2.zero, Command.Thrust); Assert.AreEqual(0, Command.Turn);
        }
        [Test] public void Edge_CountsOnce()
        { Frame(Key.F); Frame(Key.F); Frame(Key.F); Assert.AreEqual(1, Command.Edges.Interact); }
        [Test] public void Cruise_Counts()
        { Frame(Key.J); Frame(Key.J); Frame(Key.J); Assert.AreEqual(1, Command.Edges.Cruise); }
        [Test] public void Aim_ScreenCenter() { Camera(); Frame(); AssertAim(new float3(20, 0, 30)); }
        [Test] public void Aim_PicksCollider() { Camera(); Box(false); Frame(); AssertAim(new float3(20, 105, 30)); }
        [Test] public void Aim_IgnoresPlayer() { Camera(); Box(true); Frame(); AssertAim(new float3(20, 0, 30)); }
        [Test] public void Aim_PlaneFallback() { Camera(); Frame(); AssertAim(new float3(20, 0, 30)); }
        [Test] public void PointerBlocked_SuppressesMouseFire()
        {
            gate.Allowed = false;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(100, 100) }.WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            Frame(Key.Space); Assert.AreEqual(4, Command.FireGroups);
            gate.Allowed = true; Frame(Key.Space); Assert.AreEqual(7, Command.FireGroups);
        }
        [Test] public void Unbound_Neutral()
        {
            Camera(); Frame(Key.W, Key.F, Key.LeftShift); Assert.AreEqual(1, Command.Edges.Interact);
            sampler.Unbind(); Frame(Key.W, Key.F, Key.LeftShift);
            Assert.AreEqual(float2.zero, Command.Thrust); Assert.AreEqual(0, Command.Turn); Assert.AreEqual(0, Command.FireGroups);
            Assert.AreEqual(ShipCommandFlags.None, Command.Flags); Assert.AreEqual(0, Command.AimValid); Assert.AreEqual(1, Command.Edges.Interact);
        }
        [Test] public void NoAlloc()
        {
            Camera(); Box(false); Frame(Key.W);
            for (int i = 0; i < 10; i++) sampler.Update();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) sampler.Update();
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
