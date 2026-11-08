using Game.Core;
using Game.Core.Systems;
using Game.Input;
using Game.Presentation.Components;
using Game.Simulation.Commands;
using Game.Voxel.Grid;
using Game.Voxel.Model;
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
    // OriginRebaseSystem is OrderFirst in GameInitGroup, so a rebase precedes this sampler.
    public partial class PlayerInputSampleSystem : SystemBase
    {
        Entity commandEntity;
        EntityQuery players, cameras, physics, assistGrids, relations;
        Entity assistTarget;
        Entity assistHeld;   // last frame's assist target: it keeps its wider release radius (Snap)
        /// <summary>Auto-aim (owner, 2026-10-21: "auto-aiming for players enabled by default"): a cursor within
        /// <see cref="AssistRadius"/> of a hostile ship aims at that ship, and the weapons lead it. Dev switch until Settings has it.</summary>
        [Game.Core.StaticStateOk("dev switch for auto-aim, main thread")]
        public static bool AimAssist = true;
        /// <summary>Snap and release distances as fractions of the screen height (about 50 and 75 px at 1080p; Retina-safe).</summary>
        public const float AssistRadius = .045f, AssistRelease = .07f;
        /// <summary>The ship auto-aim is on this frame, or Entity.Null (for the HUD reticle).</summary>
        public Entity AssistTarget => assistTarget;
        /// <summary>The grid under the cursor ray this frame (any relation), or the auto-aim target; the HUD tags what the player points at.</summary>
        public Entity HoverEntity { get; private set; }
        InputService input;
        IWorldPointerGate pointerGate;
        InputAction thrust, strafe, turn, strafeModifier, boost, brake, firePrimary, fireSecondary, fireTertiary;
        InputAction interact, ability1, ability2, ability3, ability4, targetNext, targetUnderCursor, targetClear, autofire, cruise;
        InputAction aimDirection;
        InputAction lift, faceAimToggle, faceTargetToggle;   // ADR-0123 experimental 3D flight
        bool faceAim, faceTarget;                           // the toggles, kept only while the player ship is in 3D flight
        string savedPreset;                                 // CAM-013: the flight camera preset to restore after free aim
        Entity aimPlayer;
        float2 lastGamepadDirection;
        static readonly ProfilerMarker Sample = new ProfilerMarker(ProfilerCategory.Scripts, "PlayerInputSampleSystem.Sample");
        protected override void OnCreate()
        {
            commandEntity = EntityManager.CreateSingleton<PlayerCommand>();
            players = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<PlayerShip>() }, None = new[] { ComponentType.ReadOnly<Parent>() } });
            cameras = GetEntityQuery(ComponentType.ReadOnly<MainCameraState>());
            physics = GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            assistGrids = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<Game.Simulation.Allegiance>() },
                None = new[] { ComponentType.ReadOnly<Game.Voxel.Damage.Hulk>(), ComponentType.ReadOnly<Parent>() }
            });
            relations = GetEntityQuery(ComponentType.ReadOnly<Game.Simulation.FactionRelations>());
            RequireForUpdate<PlayerCommand>();
        }
        public void Bind(InputService service, IWorldPointerGate gate)
        {
            input = service; pointerGate = gate;
            aimPlayer = Entity.Null; lastGamepadDirection = float2.zero;
            if (input == null) return;
            var flight = input.Controls.Flight;
            strafeModifier = flight.Get().FindAction("StrafeModifier", true);
            lift = flight.Get().FindAction("Lift", true);
            faceAimToggle = flight.Get().FindAction("FaceAimToggle", true); faceTargetToggle = flight.Get().FindAction("FaceTargetToggle", true);
            faceAim = faceTarget = false;
            aimDirection = flight.AimDirection;
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
            // HUD-022: auto-aim holds a ship only while the mouse aim chooses it this frame. Without this, a frame the mouse is not
            // sampled (flight controls off under a menu or prompt, a gamepad, no camera yet) kept the old ship, and the HUD showed
            // the locked reticle on a ship that had since died.
            assistHeld = assistTarget; assistTarget = Entity.Null;
            if (input != null && input.Controls.Flight.enabled)
            {
                command.Thrust = math.clamp(new float2(strafe.ReadValue<float>(), thrust.ReadValue<float>()), -1, 1);
                command.Turn = math.clamp(turn.ReadValue<float>(), -1, 1);
                if (strafeModifier.IsPressed())
                {
                    command.Thrust.x = math.clamp(command.Thrust.x + command.Turn, -1, 1);
                    command.Turn = 0;
                }
                else if (boost.IsPressed()) command.Flags |= ShipCommandFlags.Boost;
                if (brake.IsPressed()) command.Flags |= ShipCommandFlags.Brake;
                Sample3D(ref command);
                bool allowed = false;
                if (input.CurrentControlScheme == "Gamepad") AimGamepad(ref command);
                else if (Mouse.current != null)
                {
                    float2 pointer = Mouse.current.position.ReadValue();
                    // Feel prototype `orbit` preset: the cursor is locked, and the fixed reticle aims.
                    if (Game.Presentation.Services.FlightCameraPreset.Current.MouseLook && !cameras.IsEmptyIgnoreFilter)
                    {
                        cameras.CompleteDependency(); var size = cameras.GetSingleton<MainCameraState>().ScreenSizePx;
                        pointer = new float2(.5f, Game.Presentation.Services.FlightCameraPreset.Current.ReticleHeight) * size;
                    }
                    allowed = pointerGate == null || pointerGate.IsWorldPointerAllowed(pointer);
                    Aim(pointer, ref command);
                }
                command.FireGroups = (firePrimary.IsPressed() && (allowed || NonPointerBindingPressed(firePrimary)) ? 1u : 0) | (fireSecondary.IsPressed() && (allowed || NonPointerBindingPressed(fireSecondary)) ? 2u : 0) | (fireTertiary.IsPressed() ? 4u : 0);
                Count(interact, ref command.Edges.Interact); Count(ability1, ref command.Edges.Ability1);
                Count(ability2, ref command.Edges.Ability2); Count(ability3, ref command.Edges.Ability3); Count(ability4, ref command.Edges.Ability4);
                Count(targetNext, ref command.Edges.TargetNext); Count(targetClear, ref command.Edges.TargetClear);
                if (allowed || targetUnderCursor.activeControl?.device is Gamepad) Count(targetUnderCursor, ref command.Edges.TargetUnderCursor);
                Count(autofire, ref command.Edges.AutofireToggle); Count(cruise, ref command.Edges.Cruise);
            }
            EntityManager.SetComponentData(commandEntity, command);
        }
        static bool NonPointerBindingPressed(InputAction action)
        {
            foreach (var control in action.controls)
                if ((control.device is Keyboard || control.device is Gamepad) && control is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed) return true;
            return false;
        }
        static void Count(InputAction action, ref byte counter)
        { if (action.WasPressedThisFrame()) counter = unchecked((byte)(counter + 1)); }
        void AimGamepad(ref PlayerCommand command)
        {
            players.CompleteDependency();
            if (players.CalculateEntityCount() != 1) return;
            Entity player = players.GetSingletonEntity();
            if (!EntityManager.HasComponent<LocalTransform>(player)) return;
            EntityManager.CompleteDependencyBeforeRO<LocalTransform>();
            var transform = EntityManager.GetComponentData<LocalTransform>(player);
            if (!math.all(math.isfinite(transform.Position))) return;
            if (aimPlayer != player)
            {
                aimPlayer = player;
                float3 forward = math.rotate(transform.Rotation, new float3(0, 0, 1));
                lastGamepadDirection = math.normalizesafe(forward.xz, new float2(0, 1));
            }
            float2 direction = aimDirection.ReadValue<UnityEngine.Vector2>();
            if (math.all(math.isfinite(direction)) && math.lengthsq(direction) > 0)
                lastGamepadDirection = math.normalize(direction);
            // Rebuild from the current ship position, including while the stick is released.
            command.AimWorld = transform.Position + new float3(lastGamepadDirection.x, 0, lastGamepadDirection.y) * 50;
            command.AimValid = 1;
        }
        void Aim(float2 pointer, ref PlayerCommand command)
        {
            HoverEntity = Entity.Null;
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
                {
                    Entity hitEntity = collector.ClosestHit.Entity;
                    HoverEntity = hitEntity;
                    // A ray straight onto a hostile hull keeps the exact cell the player points at; anything else may snap.
                    if (AimAssist && IsAssistCandidate(hitEntity, out _)) { assistTarget = hitEntity; SetAim(ref command, collector.ClosestHit.Position); return; }
                    if (Snap(pointer, camera, ref command)) { HoverEntity = assistTarget; return; }
                    assistTarget = Entity.Null; SetAim(ref command, collector.ClosestHit.Position); return;
                }
            }
            if (Snap(pointer, camera, ref command)) { HoverEntity = assistTarget; return; }
            assistTarget = Entity.Null;
            // CAM-013: in free aim the reticle ray that hits nothing aims far ahead, where the view looks.
            if (faceAim && Game.Presentation.Services.FlightCameraPreset.Current.Name == Game.Presentation.Services.FreeAim.PresetName)
            { SetAim(ref command, Game.Presentation.Services.FreeAim.FarPoint(near, direction)); return; }
            // ADR-0123: a ship off the plane aims on the plane through itself.
            if (Planar.RayPlaneY(near, direction, Flying3D(out float3 position) ? position.y : 0, out var hit, out _)) SetAim(ref command, hit);
        }

        /// <summary>ADR-0123: Lift and the facing toggles, only while the player ship flies in 3D; leaving 3D clears both toggles.
        /// The toggles are independent (owner, 2026-11-04: "toggling off P shouldn't also toggle off B"); with both on, FaceTarget wins
        /// in PropulsionJob while there is a target, and turning it off goes back to following the mouse.</summary>
        void Sample3D(ref PlayerCommand command)
        {
            if (!Flying3D(out _)) { faceAim = faceTarget = false; FreeAimCamera(false); return; }
            command.Lift = math.clamp(lift.ReadValue<float>(), -1, 1);
            if (faceAimToggle.WasPressedThisFrame()) faceAim = !faceAim;
            if (faceTargetToggle.WasPressedThisFrame()) faceTarget = !faceTarget;
            if (faceAim) command.Flags |= ShipCommandFlags.FaceAim;
            if (faceTarget) command.Flags |= ShipCommandFlags.FaceTarget;
            FreeAimCamera(faceAim);
        }

        /// <summary>CAM-013: face-the-mouse in 3D flies with the free-aim chase camera; turning it off brings the player's preset back.</summary>
        void FreeAimCamera(bool active)
        {
            string next = Game.Presentation.Services.FreeAim.Switch(active, Game.Presentation.Services.FlightCameraPreset.Current.Name, ref savedPreset);
            if (next != null) Game.Presentation.Services.FlightCameraPreset.TrySet(next);
        }

        bool Flying3D(out float3 position)
        {
            position = default;
            players.CompleteDependency();
            if (players.CalculateEntityCount() != 1) return false;
            Entity player = players.GetSingletonEntity();
            EntityManager.CompleteDependencyBeforeRO<Game.Data.Components.Flight3D>();
            if (!EntityManager.HasComponent<Game.Data.Components.Flight3D>(player) || !EntityManager.IsComponentEnabled<Game.Data.Components.Flight3D>(player)) return false;
            if (EntityManager.HasComponent<LocalTransform>(player))
            { EntityManager.CompleteDependencyBeforeRO<LocalTransform>(); position = EntityManager.GetComponentData<LocalTransform>(player).Position; }
            return true;
        }

        static void SetAim(ref PlayerCommand command, float3 point)
        {
            command.AimWorld = point; command.AimValid = 1;
            if (AimAssist) command.Flags |= ShipCommandFlags.AimAssist;
        }

        /// <summary>Snaps a near miss onto the hostile ship closest to the cursor on screen, measured from the edge of its projected
        /// hull sphere. The current target holds until the cursor is <see cref="AssistRelease"/> away, so it does not flicker.</summary>
        bool Snap(float2 pointer, in MainCameraState camera, ref PlayerCommand command)
        {
            if (!AimAssist) return false;
            Entity best = Entity.Null; float bestPx = float.MaxValue; float3 bestPoint = default;
            assistGrids.CompleteDependency();
            using var entities = assistGrids.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!IsAssistCandidate(entity, out float3 center) || !Project(camera, center, out float2 screen, out float depth)) continue;
                var grid = EntityManager.GetComponentData<VoxelGrid>(entity);
                float radius = math.length((float3)(grid.CellMax + 1 - grid.CellMin)) * grid.CellSize * .5f;
                float radiusPx = radius * .7f * camera.ScreenSizePx.y / (2 * depth * math.tan(math.radians(camera.VerticalFovDeg) * .5f));
                float px = math.max(0, math.distance(screen, pointer) - radiusPx);
                float limit = (entity == assistHeld ? AssistRelease : AssistRadius) * camera.ScreenSizePx.y;
                if (px > limit || px >= bestPx) continue;
                best = entity; bestPx = px; bestPoint = center;
            }
            assistTarget = best;
            if (best == Entity.Null) return false;
            SetAim(ref command, bestPoint);
            return true;
        }

        bool IsAssistCandidate(Entity entity, out float3 center)
        {
            center = default;
            if (entity == Entity.Null || !assistGrids.Matches(entity) || players.Matches(entity)) return false;
            if (relations.IsEmptyIgnoreFilter || players.CalculateEntityCount() != 1) return false;
            Entity player = players.GetSingletonEntity();
            if (!EntityManager.HasComponent<Game.Simulation.Allegiance>(player)) return false;
            var own = EntityManager.GetComponentData<Game.Simulation.Allegiance>(player);
            if (!relations.GetSingleton<Game.Simulation.FactionRelations>().IsHostile(own, EntityManager.GetComponentData<Game.Simulation.Allegiance>(entity))) return false;
            var ltw = EntityManager.GetComponentData<LocalToWorld>(entity);
            if (EntityManager.HasComponent<PhysicsMass>(entity)) center = math.transform(ltw.Value, EntityManager.GetComponentData<PhysicsMass>(entity).CenterOfMass);
            else
            {
                var grid = EntityManager.GetComponentData<VoxelGrid>(entity);
                center = math.transform(ltw.Value, ((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize));
            }
            return math.all(math.isfinite(center));
        }

        static bool Project(in MainCameraState camera, float3 point, out float2 screen, out float depth)
        {
            float4 clip = math.mul(camera.ViewProjection, new float4(point, 1));
            screen = default; depth = clip.w;
            if (!(clip.w > camera.NearClip)) return false;
            screen = (clip.xy / clip.w * .5f + .5f) * camera.ScreenSizePx;
            return math.all(math.isfinite(screen));
        }
    }
}
