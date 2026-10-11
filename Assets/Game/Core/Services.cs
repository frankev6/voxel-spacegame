using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Typed service locator (architecture §4.2). Main thread only. Implements SPEC-02 §4.4, §5.3: one static slot per type,
    /// so an interface and a concrete registration of the same object are independent slots.</summary>
    public static class Services
    {
        static class Slot<T> where T : class { internal static T Instance; internal static bool Tracked; }

        static readonly List<Action> resetters = new List<Action>(64);   // one per type ever registered since the last Reset
        static int count;

        /// <summary>ArgumentNullException; InvalidOperationException if T already registered (the first instance stays).</summary>
        public static T Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (Slot<T>.Instance != null) throw new InvalidOperationException("Service " + typeof(T).Name + " is already registered");
            Slot<T>.Instance = service;
            count++;
            if (!Slot<T>.Tracked)
            {
                Slot<T>.Tracked = true;
                resetters.Add(static () => { Slot<T>.Instance = null; Slot<T>.Tracked = false; });
            }
            return service;
        }

        public static T Get<T>() where T : class
            => Slot<T>.Instance ?? throw new InvalidOperationException("Service " + typeof(T).Name + " is not registered");

        public static bool TryGet<T>(out T service) where T : class { service = Slot<T>.Instance; return service != null; }   // O(1), 0 B
        public static bool IsRegistered<T>() where T : class => Slot<T>.Instance != null;

        /// <summary>True only if the registered instance is reference-equal to service.</summary>
        public static bool Unregister<T>(T service) where T : class
        {
            if (service == null || !ReferenceEquals(Slot<T>.Instance, service)) return false;
            Slot<T>.Instance = null;
            count--;
            return true;
        }

        public static int Count => count;

        /// <summary>Clears every slot; does not dispose.</summary>
        public static void Reset()
        {
            foreach (var r in resetters) r();
            resetters.Clear();
            count = 0;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Reset();
    }
}
