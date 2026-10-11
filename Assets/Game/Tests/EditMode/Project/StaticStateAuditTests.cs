using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Game.Tests.EditMode.Project
{
    /// <summary>FND-014: with domain reload off, every static field in runtime Game.* code must reset itself, be immutable, or say why it
    /// may survive a Play Mode exit (SPEC-01 FND-014, decision 13 context).</summary>
    [Category("Project")]
    public sealed class StaticStateAuditTests
    {
        /// <summary>Runtime assemblies: editor tooling and tests never run inside Play Mode's lifetime.</summary>
        internal static IEnumerable<Assembly> RuntimeGameAssemblies() => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name.StartsWith("Game.", StringComparison.Ordinal))
            .Where(a => !a.GetName().Name.Contains(".Editor") && !a.GetName().Name.StartsWith("Game.Tests", StringComparison.Ordinal))
            .OrderBy(a => a.GetName().Name);

        static bool IsImmutable(Type t)
        {
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Type) || t == typeof(decimal)) return true;
            if (t == typeof(Unity.Profiling.ProfilerMarker)) return true;
            if (t.IsGenericType)
            {
                var g = t.GetGenericTypeDefinition();
                if (g == typeof(Unity.Burst.SharedStatic<>) || g == typeof(System.Collections.ObjectModel.ReadOnlyCollection<>)) return true;
                if (g.FullName == "System.Collections.Immutable.ImmutableArray`1") return true;
            }
            // A readonly field of an unmanaged struct (no references) cannot change after the static constructor.
            return t.IsValueType && !t.ContainsGenericParameters && UnsafeUtility.IsUnmanaged(t);
        }

        static bool HasSubsystemReset(Type t) => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Any(m => m.GetCustomAttributes<RuntimeInitializeOnLoadMethodAttribute>().Any(a => a.loadType == RuntimeInitializeLoadType.SubsystemRegistration));

        internal static List<string> Violations(IEnumerable<Type> types)
        {
            var result = new List<string>();
            foreach (var type in types)
            {
                if (type.IsDefined(typeof(CompilerGeneratedAttribute), false) || type.Name.StartsWith("<", StringComparison.Ordinal)) continue;   // lambdas, iterators
                if (type.Name.Contains("$") || type.Name.Contains("__codegen__")) continue;                  // Burst direct-call stubs
                if (type.Namespace == null || !type.Namespace.StartsWith("Game", StringComparison.Ordinal)) continue;   // source-generated registries
                bool resets = HasSubsystemReset(type);
                foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsLiteral) continue;                                                               // const
                    bool backing = f.Name.Contains("k__BackingField");                                       // static auto-properties are audited
                    if (f.IsDefined(typeof(CompilerGeneratedAttribute), false) && !backing) continue;
                    if (resets) continue;
                    if (f.IsInitOnly && IsImmutable(f.FieldType)) continue;
                    if (f.IsDefined(typeof(StaticStateOkAttribute), false)) continue;
                    result.Add($"{type.FullName}.{f.Name} ({f.FieldType.Name}{(f.IsInitOnly ? ", readonly" : "")})");
                }
            }
            return result;
        }

        static IEnumerable<Type> SafeTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        [Test]
        public void AllStaticsReset()
        {
            var assemblies = RuntimeGameAssemblies().ToList();
            Assert.GreaterOrEqual(assemblies.Count, 10, "the runtime Game.* assemblies are loaded");
            var violations = Violations(assemblies.SelectMany(SafeTypes));
            Assert.IsEmpty(violations, "Static fields that survive a Play Mode exit without a SubsystemRegistration reset, an immutable readonly type " +
                                       "or [StaticStateOk(\"reason\")]:\n" + string.Join("\n", violations));
        }

        // The acceptance check "adding static int x fails the audit with the field name", without touching runtime code.
        static class Offender { public static int x; }
        static class ResetOffender { public static int y; [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { y = 0; } }
        static class AutoOffender { public static int Count { get; set; } }

        [Test]
        public void DetectsAMutableStatic()
        {
            var v = Violations(new[] { typeof(Offender), typeof(ResetOffender), typeof(AutoOffender) });
            Assert.AreEqual(2, v.Count, string.Join("\n", v));
            StringAssert.Contains("Offender.x", v[0]);
            StringAssert.Contains("AutoOffender.<Count>k__BackingField", v[1]);
        }
    }
}
