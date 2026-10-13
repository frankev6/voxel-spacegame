using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Galaxy.Services
{
    /// <summary>Ordered frame services; mutations during a tick affect the next tick.</summary>
    public sealed class GalaxyFrameTicker
    {
        readonly List<IGalaxyFrameTick> services = new(), snapshot = new();
        bool ticking;
        public void Register(IGalaxyFrameTick service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (services.Contains(service)) return;
            services.Add(service);
            services.Sort((a, b) =>
            {
                int order = a.TickOrder.CompareTo(b.TickOrder);
                return order != 0 ? order : string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
            });
        }
        public void Unregister(IGalaxyFrameTick service) => services.Remove(service);
        public void Tick(in GalaxyFrameContext context)
        {
            if (ticking) throw new InvalidOperationException("Galaxy frame ticks cannot be nested.");
            ticking = true;
            snapshot.Clear(); snapshot.AddRange(services);
            try
            {
                foreach (var service in snapshot)
                    try { service.Tick(context); }
                    catch (Exception error) { GameLog.Error("Galaxy", service.GetType().FullName + " tick failed: " + error); }
            }
            finally { snapshot.Clear(); ticking = false; }
        }
    }
}
