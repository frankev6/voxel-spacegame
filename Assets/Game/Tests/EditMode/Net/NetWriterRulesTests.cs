using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.Net
{
    /// <summary>ADR-0117 §4 and §7: Game.Net only transports and translates. It may decode the host's decisions into the records and
    /// replicated state that the shared appliers consume, but it may not write cells, emit effects, sound or events, or add the
    /// components whose consequences an applier owns. Checked on the compiled IL of every method in Game.Net, lambdas and state
    /// machines included, so a new call cannot slip in by name.</summary>
    public sealed class NetWriterRulesTests
    {
        // Components Game.Net may add or write, and why. Anything else is a consequence that belongs to an applier.
        static readonly Dictionary<string, string> AllowedComponents = new()
        {
            ["Game.Net.Replication.Replicated"] = "the network layer's own bookkeeping",
            ["Game.Core.Net.PilotName"] = "replicated state: a pilot's display name",
            ["Game.Simulation.Commands.Pilot"] = "replicated state: which slot flies the ship",
            ["Game.Simulation.Allegiance"] = "replicated state: the ship's side",
            ["Game.Galaxy.Salvage.SalvageCollected"] = "replicated decision: the host's salvage; SalvageMark/Pull/Despawn apply it",
            ["Game.Simulation.Ai.PilotCombatStatus"] = "replicated decision: the host's combat status for a pilot",
            ["Game.Simulation.Abilities.AbilityHostState"] = "record for AbilitySlotApplySystem",
            ["Unity.Transforms.LocalTransform"] = "replica pose from snapshots",
            ["Unity.Physics.PhysicsVelocity"] = "replica velocity from snapshots",
            ["Game.Simulation.Commands.ShipCommand"] = "replica command from snapshots (cosmetic weapons fire on guests)",
            ["Game.Simulation.Commands.RemotePilotInput"] = "on the host: a guest's sampled command, the input to the shared command conversion",
            ["Game.Simulation.Weapons.ShipTarget"] = "a pilot's target lock is decided on that pilot's machine (ADR-0117 note 4); AI aim on guests",
            ["Game.Simulation.Weapons.WeaponControl"] = "autofire and aim mode, decided with the target lock",
            ["Game.Simulation.Power.ShieldState"] = "shield HP and state from snapshots",
            ["Game.Simulation.Flight.ThrusterState"] = "replica plumes from snapshots (no flight solve for kinematic replicas)",
            ["Game.Voxel.Model.VoxelGrid"] = "grid kind changes other than hulk transitions, which go through HulkTransitionSystem",
            ["Game.Voxel.Model.GridSource"] = "a replica's spawn source flags",
        };

        // Replica helpers Game.Net may call: reads, the palette (state with no consequences) and chunk bodies through ChunkBodySetup.
        static readonly HashSet<string> AllowedReplicaApi = new()
        { "CreateStructure", "SetPalette", "CollectQuietBricks", "ReadBrick", "CreateChunk", "DestroyChunk", "HashCells" };

        static readonly HashSet<string> EntityWriters = new()
        {
            "AddComponent", "AddComponentData", "AddBuffer", "AddSharedComponent", "AddSharedComponentManaged", "AddComponentObject",
            "RemoveComponent", "SetComponentData", "SetComponentEnabled", "SetSharedComponent", "SetSharedComponentManaged", "SetBuffer",
            "DestroyEntity", "Instantiate"
        };

        [Test] public void GameNet_WritesOnlyRecordsAndReplicatedState()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Game.Net");
            var violations = new List<string>();
            int methods = 0, calls = 0;
            foreach (var type in assembly.GetTypes())
                foreach (var method in Methods(type))
                {
                    methods++;
                    foreach (var callee in Calls(method)) { calls++; if (Violation(callee) is string why) violations.Add(Name(method) + " → " + Name(callee) + ": " + why); }
                }
            Assert.Greater(methods, 50, "the scan reached Game.Net's methods");
            Assert.Greater(calls, 500, "the IL decoder found Game.Net's calls");
            CollectionAssert.IsEmpty(violations.Distinct().OrderBy(v => v).ToList(),
                "Game.Net must translate, not apply (ADR-0117 §4). Route the consequence through a shared applier, or add the component to AllowedComponents with its reason.");
        }

        [Test] public void Rules_FlagConsequences()
        {
            MethodInfo Generic(string name, Type argument) => typeof(EntityManager).GetMethods()
                .First(m => m.Name == name && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Entity))
                .MakeGenericMethod(argument);
            Assert.IsNotNull(Violation(Generic("AddComponent", typeof(Game.Voxel.Damage.Hulk))), "adding Hulk is HulkTransitionSystem's");
            Assert.IsNotNull(Violation(Generic("AddComponent", typeof(Game.Galaxy.Interaction.Interactable))), "prompts are derived or applied");
            Assert.IsNull(Violation(Generic("AddComponent", typeof(Game.Simulation.Commands.Pilot))), "Pilot is replicated state");
            Assert.IsNotNull(Violation(typeof(Game.Core.Events.EventBus<Game.Core.Events.EconomyNotice>).GetMethod("Publish")), "events");
            Assert.IsNotNull(Violation(typeof(EntityManager).GetMethod("DestroyEntity", new[] { typeof(Entity) })), "destroying entities");
            var cellWriter = typeof(Game.Voxel.Damage.Hulk).Assembly.GetType("Game.Voxel.Damage.Jobs.CellWriter");
            Assert.IsNotNull(cellWriter, "CellWriter moved: update the rule");
            Assert.IsNotNull(Violation(cellWriter.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).First()), "cells");
            Assert.IsNotNull(Violation(typeof(Game.Core.Audio.IAudioService).GetMethod("PlayCue")), "sound");
        }

        static string Violation(MethodBase callee)
        {
            var type = callee.DeclaringType;
            if (type == null) return null;
            string full = type.FullName ?? type.Name, generic = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : full;
            if (full.StartsWith("Game.Voxel.Damage.Jobs.CellWriter", StringComparison.Ordinal)) return "cells are written by the shared CellWriter applier";
            if (full == "Game.Voxel.Replica.ReplicaVoxelApi" && !AllowedReplicaApi.Contains(callee.Name)) return "not an allowed replica helper";
            if (full == "Game.Core.Audio.IAudioService") return "sound is an applier's consequence";
            if (generic == "Game.Core.Events.EventBus`1" && callee.Name == "Publish") return "events are published by the systems that own them";
            if (callee.Name == "Enqueue" && type.IsGenericType && type.GetGenericArguments().Any(a => a.FullName is "Game.Voxel.PresentationStreams.FxEvent" or "Game.Voxel.PresentationStreams.SfxEvent"))
                return "effects and sound come from appliers";
            if (type == typeof(EntityManager) && EntityWriters.Contains(callee.Name))
            {
                if (callee.Name is "DestroyEntity" or "Instantiate") return "entities are created and destroyed through spawn requests and appliers";
                if (!callee.IsGenericMethod) return "only typed writes of allowed components";
                foreach (var argument in callee.GetGenericArguments())
                    if (!AllowedComponents.ContainsKey(argument.FullName ?? argument.Name)) return argument.FullName + " is not replicated state or a record";
            }
            return null;
        }

        static IEnumerable<MethodBase> Methods(Type type)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(all)) yield return method;
            foreach (var constructor in type.GetConstructors(all)) yield return constructor;
            var initializer = type.TypeInitializer; if (initializer != null) yield return initializer;
        }

        static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);

        /// <summary>Every method a method body calls or constructs (call, callvirt, newobj, ldftn, ldvirtftn).</summary>
        static IEnumerable<MethodBase> Calls(MethodBase method)
        {
            byte[] il;
            try { il = method.GetMethodBody()?.GetILAsByteArray(); } catch { yield break; }
            if (il == null) yield break;
            Type[] typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
            for (int i = 0; i < il.Length;)
            {
                short value = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[i + 1]) : il[i];
                if (!OpCodesByValue.TryGetValue(value, out var op)) yield break;   // unknown byte: stop rather than misread
                i += op.Size;
                int operand = op.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                    _ => 4
                };
                if (op.OperandType == OperandType.InlineMethod)
                {
                    MethodBase callee = null;
                    try { callee = method.Module.ResolveMethod(BitConverter.ToInt32(il, i), typeArgs, methodArgs); } catch { }
                    if (callee != null) yield return callee;
                }
                i += operand;
            }
        }

        static string Name(MethodBase method) => (method.DeclaringType?.FullName ?? "?") + "." + method.Name +
            (method.IsGenericMethod ? "<" + string.Join(",", method.GetGenericArguments().Select(a => a.Name)) + ">" : "");
    }
}
