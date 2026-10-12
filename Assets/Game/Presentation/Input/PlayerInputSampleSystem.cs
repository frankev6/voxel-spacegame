using Game.Core;
using Game.Core.Systems;
using Game.Input;
using Game.Presentation.Components;
using Game.Simulation.Commands;
using Game.Voxel.Grid;
using Game.Voxel.PhysicsBody;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine.InputSystem;

namespace Game.Presentation.Input
{
    /// <summary>Samples the active Flight action map once per frame into latched commands (SPEC-03 §5.2).</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateBefore(typeof(GridSpawnSystem))]
    // TODO(GAL-009): OriginRebaseSystem must run before this sampler when that system is implemented.
    public partial class PlayerInputSampleSystem : SystemBase
    {
        Entity commandEntity;
        EntityQuery players, cameras, physics;
        InputService input;
        IWorldPointerGate pointerGate;
        InputAction thrust, strafe, turn, boost, brake, firePrimary, fireSecondary, fireTertiary;
        InputAction interact, ability1, ability2, ability3, ability4, targetNext, targetUnderCursor, targetClear, autofire, cruise;
        static readonly ProfilerMarker Sample = new ProfilerMarker(ProfilerCategory.Scripts, "PlayerInputSampleSystem.Sample");
        protected override void OnCreate()
        {
            commandEntity = EntityManager.CreateSingleton<PlayerCommand>();
            players = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<PlayerShip>() }, None = new[] { ComponentType.ReadOnly<Parent>() } });
            cameras = GetEntityQuery(ComponentType.ReadOnly<MainCameraState>());
            physics = GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            RequireForUpdate<PlayerCommand>();
        }
        public void Bind(InputService service, IWorldPointerGate gate)
        {
            input = service; pointerGate = gate;
            if (input == null) return;
            var flight = input.Controls.Flight;
            thrust = flight.Thrust; strafe = flight.Strafe; turn = flight.Turn; boost = flight.Boost; brake = flight.Brake;
            firePrimary = flight.FirePrimary; fireSecondary = flight.FireSecondary; fireTertiary = flight.FireTertiary;
            interact = flight.Interact; ability1 = flight.Ability1; ability2 = flight.Ability2; ability3 = flight.Ability3; ability4 = flight.Ability4;
            targetNext = flight.TargetNext; targetUnderCursor = flight.TargetUnderCursor; targetClear = flight.TargetClear;
            autofire = flight.AutofireToggle; cruise = flight.Cruise;
        }
        public void Unbind() => Bind(null, null);
        protected override void OnUpdate()
        {
            using var marker = Sample.Auto();
            EntityManager.CompleteDependencyBeforeRW<PlayerCommand>();
            var previous = EntityManager.GetComponentData<PlayerCommand>(commandEntity);
            var command = new PlayerCommand { Edges = previous.Edges, AimWorld = previous.AimWorld, FrameIndex = (uint)UnityEngine.Time.frameCount };
            if (input != null && input.Controls.Flight.enabled)
            {
                command.Thrust = math.clamp(new float2(strafe.ReadValue<float>(), thrust.ReadValue<float>()), -1, 1);
                command.Turn = math.clamp(turn.ReadValue<float>(), -1, 1);
                if (boost.IsPressed()) command.Flags |= ShipCommandFlags.Boost;
                if (brake.IsPressed()) command.Flags |= ShipCommandFlags.Brake;
                bool allowed = false;
                if (Mouse.current != null)
                {
                    var pointer = Mouse.current.position.ReadValue();
                    allowed = pointerGate == null || pointerGate.IsWorldPointerAllowed(pointer);
                    Aim(pointer, ref command);
                }
                command.FireGroups = (firePrimary.IsPressed() && allowed ? 1u : 0) | (fireSecondary.IsPressed() && allowed ? 2u : 0) | (fireTertiary.IsPressed() ? 4u : 0);
                Count(interact, ref command.Edges.Interact); Count(ability1, ref command.Edges.Ability1);
                Count(ability2, ref command.Edges.Ability2); Count(ability3, ref command.Edges.Ability3); Count(ability4, ref command.Edges.Ability4);
                Count(targetNext, ref command.Edges.TargetNext); Count(targetClear, ref command.Edges.TargetClear);
                if (allowed) Count(targetUnderCursor, ref command.Edges.TargetUnderCursor);
                Count(autofire, ref command.Edges.AutofireToggle); Count(cruise, ref command.Edges.Cruise);
            }
            EntityManager.SetComponentData(commandEntity, command);
        }
        static void Count(InputAction action, ref byte counter)
        { if (action.WasPressedThisFrame()) counter = unchecked((byte)(counter + 1)); }
        void Aim(float2 pointer, ref PlayerCommand command)
        {
            if (cameras.IsEmptyIgnoreFilter) return;
            cameras.CompleteDependency(); var camera = cameras.GetSingleton<MainCameraState>();
            if (math.any(camera.ScreenSizePx <= 0)) return;
            float2 ndc = pointer / camera.ScreenSizePx * 2 - 1;
            var inverse = math.inverse(camera.ViewProjection);
            float4 a = math.mul(inverse, new float4(ndc, -1, 1)), b = math.mul(inverse, new float4(ndc, 1, 1));
            float3 near = a.xyz / a.w, far = b.xyz / b.w;
            if (!math.all(math.isfinite(near)) || !math.all(math.isfinite(far))) return;
            float3 direction = math.normalizesafe(far - near);
            if (!physics.IsEmptyIgnoreFilter)
            {
                // Singleton access does not complete dependencies implicitly (T2.9); wait for the physics world writer explicitly.
                physics.CompleteDependency(); var world = physics.GetSingleton<PhysicsWorldSingleton>();
                Entity ignore = Entity.Null; int count = players.CalculateEntityCount();
                if (count == 1) ignore = players.GetSingletonEntity();
                else if (count > 1) { using var entities = players.ToEntityArray(Allocator.Temp); ignore = entities[0]; }
                var collector = new IgnoreBodyClosestHitCollector(ignore, 1);
                if (world.CastRay(new RaycastInput { Start = near, End = near + direction * 5000, Filter = PhysicsLayers.PickingQuery }, ref collector))
                { command.AimWorld = collector.ClosestHit.Position; command.AimValid = 1; return; }
            }
            if (Planar.RayPlaneY(near, direction, 0, out var hit, out _)) { command.AimWorld = hit; command.AimValid = 1; }
        }
    }
}
