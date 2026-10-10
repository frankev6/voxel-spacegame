namespace Game.Core
{
    /// <summary>Typed service locator (architecture §4.2). Main thread only. Implements SPEC-02 §4.4, §5.3.</summary>
    public static class Services
    {
        public static T Register<T>(T service) where T : class { throw new System.NotImplementedException("CORE-005"); }   // ArgumentNullException; InvalidOperationException if T already registered
        public static T Get<T>() where T : class { throw new System.NotImplementedException("CORE-005"); }                 // InvalidOperationException("Service <T.Name> is not registered")
        public static bool TryGet<T>(out T service) where T : class { throw new System.NotImplementedException("CORE-005"); }
        public static bool IsRegistered<T>() where T : class { throw new System.NotImplementedException("CORE-005"); }
        public static bool Unregister<T>(T service) where T : class { throw new System.NotImplementedException("CORE-005"); }  // true only if the registered instance is reference-equal to service
        public static int Count => throw new System.NotImplementedException("CORE-005");
        public static void Reset() { throw new System.NotImplementedException("CORE-005"); }                               // clears every slot; does not dispose
    }
}
