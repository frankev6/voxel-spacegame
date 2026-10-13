using Game.Core.Components;
using Game.Data.Blocks;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Simulation.Flight
{
    /// <summary>Writes current-tick thruster demand, including coast and spin assistance (SPEC-20 §5.7).</summary>
    [BurstCompile, UpdateInGroup(typeof(PowerDemandGroup))]
    public partial struct ThrusterPowerDemandSystem : ISystem
    {
        ComponentLookup<ShipCommand> commands;
        ComponentLookup<PhysicsVelocity> velocities;
        ComponentLookup<PhysicsMass> masses;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            commands = state.GetComponentLookup<ShipCommand>(true);
            velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            masses = state.GetComponentLookup<PhysicsMass>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            commands.Update(ref state); velocities.Update(ref state); masses.Update(ref state);
            state.Dependency = new DemandJob { Commands = commands, Velocities = velocities, Masses = masses }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] partial struct DemandJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ShipCommand> Commands;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
            void Execute(in FunctionalBlock functional, in ThrusterState thruster, DynamicBuffer<PowerRequest> requests)
            {
                Entity grid = functional.Grid;
                var command = Commands.HasComponent(grid) ? Commands[grid] : default;
                bool moving = math.any(command.Thrust != 0) || command.Turn != 0 || (command.Flags & ShipCommandFlags.Brake) != 0;
                bool assisting = false;
                if (Velocities.HasComponent(grid) && Masses.HasComponent(grid))
                {
                    var velocity = Velocities[grid]; var mass = Masses[grid];
                    assisting = mass.InverseMass > 0 && (math.any(velocity.Linear.xz != 0) ||
                        math.rotate(mass.InertiaOrientation, velocity.Angular).y != 0);
                }
                PowerRegistration.SetRequesting(requests, BlockModuleId.Thruster, moving || assisting);
            }
        }
    }
}
