using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Net;
using Unity.Collections;
using Unity.Entities;

namespace Game.App.Session
{
    /// <summary>Which machines run a system in a co-op session (ADR-0116 §4).</summary>
    public enum SystemAuthority : byte
    {
        Both = 0,           // runs everywhere, unchanged (presentation-facing, derived stats, cosmetic weapons on guests)
        AuthorityOnly = 1,  // solo and host only: AI, damage, splits, flight forces, encounters, origin rebasing
        GuestOnly = 2,      // co-op guests only (replica apply, body sync, damage drain)
        HostOnly = 3,       // co-op host only (capture, remote pilots)
        Session = 4,        // host and guest while a session is live, never in solo (transport send)
    }

    /// <summary>ADR-0116 §4: the one place that decides which systems run for a role. Every system in the fixed step, GameInitGroup and
    /// WorldGroup is listed by name; RoleSystemPolicyTests fails for an unlisted one. Main thread.</summary>
    public static class RoleSystemPolicy
    {
        [StaticStateOk("immutable table built once")]
        public static readonly IReadOnlyDictionary<string, SystemAuthority> Table = new Dictionary<string, SystemAuthority>
        {
            // GameInitGroup
            ["OriginRebaseSystem"] = SystemAuthority.AuthorityOnly,
            ["PlayerInputSampleSystem"] = SystemAuthority.Both,
            ["PlayerCommandReplaySystem"] = SystemAuthority.Both,
            ["PlayerCommandRecordSystem"] = SystemAuthority.Both,
            ["GridSpawnSystem"] = SystemAuthority.Both,
            ["GridSpawnBodyInitSystem"] = SystemAuthority.Both,
            ["PowerInitSystem"] = SystemAuthority.Both,
            ["PowerRegistrationSystem"] = SystemAuthority.Both,
            ["ThrusterInitSystem"] = SystemAuthority.Both,
            ["WeaponInitSystem"] = SystemAuthority.Both,
            ["SalvagePullSystem"] = SystemAuthority.AuthorityOnly,
            ["SceneNavigationBridgeSystem"] = SystemAuthority.Both,
            ["RemotePilotReceiveSystem"] = SystemAuthority.HostOnly,
            ["ReplicaApplySystem"] = SystemAuthority.GuestOnly,
            // GameplayPrePhysicsGroup and its subgroups
            ["FactionStandingSystem"] = SystemAuthority.AuthorityOnly,
            ["PlayerCommandApplySystem"] = SystemAuthority.Both,          // guests: their own ship's cosmetic weapons fire at once
            ["RemotePilotApplySystem"] = SystemAuthority.HostOnly,
            ["AiAnchorShiftSystem"] = SystemAuthority.AuthorityOnly,
            ["AiDecisionSystem"] = SystemAuthority.AuthorityOnly,
            ["AiSteeringSystem"] = SystemAuthority.AuthorityOnly,
            ["FleetSystem"] = SystemAuthority.AuthorityOnly,
            ["PlayerCombatStatusSystem"] = SystemAuthority.AuthorityOnly,
            ["PlayerTargetingSystem"] = SystemAuthority.Both,             // guests: their own target lock for the HUD
            ["BodyOnRailsSystem"] = SystemAuthority.Both,
            ["ShieldPowerDemandSystem"] = SystemAuthority.Both,
            ["ThrusterPowerDemandSystem"] = SystemAuthority.Both,
            ["WeaponPowerDemandSystem"] = SystemAuthority.Both,
            ["StatusSystem"] = SystemAuthority.Both,
            ["PowerAllocationSystem"] = SystemAuthority.Both,
            ["ShipStatsAggregateSystem"] = SystemAuthority.Both,
            ["PropulsionStatsSystem"] = SystemAuthority.Both,
            ["WeaponStatsSystem"] = SystemAuthority.Both,
            ["PropulsionSystem"] = SystemAuthority.AuthorityOnly,
            ["PlanarConstraintSystem"] = SystemAuthority.AuthorityOnly,
            ["WeaponAimSystem"] = SystemAuthority.Both,
            ["WeaponFireSystem"] = SystemAuthority.Both,
            ["ProjectileShotHandlerSystem"] = SystemAuthority.Both,
            ["BeamShotHandlerSystem"] = SystemAuthority.Both,
            ["AbilitySystem"] = SystemAuthority.AuthorityOnly,
            ["ShieldToggleEffectSystem"] = SystemAuthority.AuthorityOnly,
            ["StatBoostEffectSystem"] = SystemAuthority.AuthorityOnly,
            ["ColliderSwapSystem"] = SystemAuthority.Both,
            ["GridBodyInitSystem"] = SystemAuthority.Both,
            ["ReplicaBodySyncSystem"] = SystemAuthority.GuestOnly,
            // PhysicsSimulationGroup
            ["SplitGraceContactSystem"] = SystemAuthority.AuthorityOnly,
            // GameplayPostPhysicsGroup
            ["SpatialHashBuildSystem"] = SystemAuthority.Both,
            ["DamageStreamsSystem"] = SystemAuthority.Both,
            ["ImpactEventSystem"] = SystemAuthority.AuthorityOnly,
            ["ProjectileStepSystem"] = SystemAuthority.Both,             // guests: tracers and impact FX; their damage is drained
            ["BeamStepSystem"] = SystemAuthority.Both,
            ["ExplosionSystem"] = SystemAuthority.Both,
            ["ShieldSystem"] = SystemAuthority.Both,                     // guests: hit flashes; HP is overwritten from snapshots
            ["DamageResolveSystem"] = SystemAuthority.AuthorityOnly,
            ["ReplicaDamageDrainSystem"] = SystemAuthority.GuestOnly,
            ["IntegritySystem"] = SystemAuthority.AuthorityOnly,
            ["SplitSystem"] = SystemAuthority.AuthorityOnly,
            ["ShipDeathSystem"] = SystemAuthority.AuthorityOnly,
            ["GridMassUpdateSystem"] = SystemAuthority.AuthorityOnly,     // guests' bodies are kinematic
            ["BrickDirtyScheduleSystem"] = SystemAuthority.Both,
            ["ColliderSnapshotSystem"] = SystemAuthority.Both,
            ["GameplayEndCommandBufferSystem"] = SystemAuthority.Both,
            ["SimClockAdvanceSystem"] = SystemAuthority.Both,
            ["HostCaptureSystem"] = SystemAuthority.HostOnly,
            // WorldGroup
            ["GalaxyServicesBridgeSystem"] = SystemAuthority.AuthorityOnly,
            ["InteractionQuerySystem"] = SystemAuthority.Both,
            ["SalvageMarkSystem"] = SystemAuthority.AuthorityOnly,
            ["SalvageReachSystem"] = SystemAuthority.AuthorityOnly,
            ["BodyGridWatchSystem"] = SystemAuthority.AuthorityOnly,
            ["GridLodSystem"] = SystemAuthority.Both,
            ["WeaponVisualSyncSystem"] = SystemAuthority.Both,
            // PresentationBridgeGroup (Game.Net)
            ["GuestCommandSendSystem"] = SystemAuthority.GuestOnly,
            ["NetSendSystem"] = SystemAuthority.Session,
        };

        public static bool Runs(SystemAuthority authority, SimRoleKind role) => authority switch
        {
            SystemAuthority.Both => true,
            SystemAuthority.AuthorityOnly => role != SimRoleKind.Guest,
            SystemAuthority.GuestOnly => role == SimRoleKind.Guest,
            SystemAuthority.HostOnly => role == SimRoleKind.Host,
            SystemAuthority.Session => role != SimRoleKind.Solo,
            _ => true
        };

        [StaticStateOk("cleared whenever the role returns to Solo; handles are only valid for the live world")]
        static readonly Dictionary<SystemHandle, bool> soloEnabled = new();

        /// <summary>Enables and disables listed systems for <paramref name="role"/>. Solo restores each system's own previous state.</summary>
        public static void Apply(World world, SimRoleKind role)
        {
            if (world == null || !world.IsCreated) return;
            world.EntityManager.CompleteAllTrackedJobs();
            var handles = new List<(SystemHandle Handle, Type Type)>();
            using (var unmanaged = world.Unmanaged.GetAllUnmanagedSystems(Allocator.Temp))
                foreach (var handle in unmanaged) handles.Add((handle, TypeManager.GetSystemType(world.Unmanaged.GetSystemTypeIndex(handle))));
            foreach (var system in world.Systems) handles.Add((system.SystemHandle, system.GetType()));
            int changed = 0;
            foreach (var (handle, type) in handles)
            {
                if (type == null || !Table.TryGetValue(type.Name, out var authority) || authority == SystemAuthority.Both) continue;
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(handle);
                bool net = authority != SystemAuthority.AuthorityOnly;   // Game.Net / replica systems are off in solo by construction
                if (role == SimRoleKind.Solo)
                {
                    bool previous = net ? false : soloEnabled.TryGetValue(handle, out var stored) ? stored : state.Enabled;
                    if (state.Enabled != previous) { state.Enabled = previous; changed++; }
                    continue;
                }
                if (!net && !soloEnabled.ContainsKey(handle)) soloEnabled[handle] = state.Enabled;
                bool run = Runs(authority, role) && (net || soloEnabled[handle]);
                if (state.Enabled != run) { state.Enabled = run; changed++; }
            }
            if (role == SimRoleKind.Solo) soloEnabled.Clear();
            GameLog.Info("Net", "role " + role + ": " + changed + " systems switched");
        }
    }
}
