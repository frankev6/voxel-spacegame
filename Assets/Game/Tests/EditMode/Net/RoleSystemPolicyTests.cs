using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.App.Session;
using Game.Core.Net;
using Game.Core.Systems;
using Game.Simulation.Commands;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Net
{
    /// <summary>SPEC-90 NET-003/006: every simulation system has a co-op authority; remote pilots match the local player's rules.</summary>
    [Category("Net")]
    public class RoleSystemPolicyTests
    {
        static readonly Type[] Roots = { typeof(FixedStepSimulationSystemGroup), typeof(GameInitGroup), typeof(WorldGroup) };

        static Type GroupOf(Type system) => system.GetCustomAttributes<UpdateInGroupAttribute>(false).FirstOrDefault()?.GroupType;

        static bool UnderRoot(Type system)
        {
            for (var group = GroupOf(system); group != null; group = GroupOf(group))
                if (Roots.Contains(group)) return true;
            return false;
        }

        [Test]
        public void EverySystem_IsClassified()
        {
            var missing = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (!name.StartsWith("Game.") || name.StartsWith("Game.Tests") || name.EndsWith(".Editor")) continue;
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsGenericTypeDefinition) continue;
                    bool system = typeof(ISystem).IsAssignableFrom(type) || (typeof(ComponentSystemBase).IsAssignableFrom(type) && !typeof(ComponentSystemGroup).IsAssignableFrom(type));
                    if (!system || !UnderRoot(type)) continue;
                    if (!RoleSystemPolicy.Table.ContainsKey(type.Name)) missing.Add(name + ": " + type.Name);
                }
            }
            Assert.IsEmpty(missing, "Classify these systems in RoleSystemPolicy.Table (ADR-0116 §4):\n" + string.Join("\n", missing));
        }

        [Test]
        public void Roles_RunTheRightSets()
        {
            Assert.IsTrue(RoleSystemPolicy.Runs(SystemAuthority.AuthorityOnly, SimRoleKind.Solo));
            Assert.IsTrue(RoleSystemPolicy.Runs(SystemAuthority.AuthorityOnly, SimRoleKind.Host));
            Assert.IsFalse(RoleSystemPolicy.Runs(SystemAuthority.AuthorityOnly, SimRoleKind.Guest));
            Assert.IsFalse(RoleSystemPolicy.Runs(SystemAuthority.GuestOnly, SimRoleKind.Solo));
            Assert.IsFalse(RoleSystemPolicy.Runs(SystemAuthority.HostOnly, SimRoleKind.Guest));
            Assert.IsFalse(RoleSystemPolicy.Runs(SystemAuthority.Session, SimRoleKind.Solo));
            Assert.IsTrue(RoleSystemPolicy.Runs(SystemAuthority.Session, SimRoleKind.Guest));
        }

        [Test]
        public void RemotePilot_ClampsAndConsumesOneEdgePerTick()
        {
            var consumed = new RemotePilotEdgeConsumed();
            var input = new RemotePilotInput { Command = new PlayerCommand { Thrust = new float2(3, -2), Turn = float.NaN, AimWorld = new float3(1, 2, float.PositiveInfinity), AimValid = 1, FireGroups = 5 } };
            var first = RemotePilotApplySystem.Apply(input, ref consumed);
            Assert.AreEqual(new float2(1, -1), first.Thrust); Assert.AreEqual(0, first.Turn); Assert.AreEqual(0, first.AimValid); Assert.AreEqual(5u, first.FireGroups);
            Assert.AreEqual(ShipCommandEdges.None, first.Edges, "presses before the ship existed are not replayed");
            input.Command.Edges.Ability1 = 2;   // two presses since
            Assert.AreEqual(ShipCommandEdges.Ability1, RemotePilotApplySystem.Apply(input, ref consumed).Edges);
            Assert.AreEqual(ShipCommandEdges.Ability1, RemotePilotApplySystem.Apply(input, ref consumed).Edges);
            Assert.AreEqual(ShipCommandEdges.None, RemotePilotApplySystem.Apply(input, ref consumed).Edges);
        }
    }
}
