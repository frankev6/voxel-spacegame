using Game.Core.Flow;
using Game.Galaxy.Services;

namespace Game.App.Boot
{
    /// <summary>Captures persistent world structures before fleet writeback and save serialization.</summary>
    internal sealed class GalaxyFlushWriteback : ISessionWriteback
    {
        readonly IGalaxyLiveDeltaSink sink;
        internal GalaxyFlushWriteback(IGalaxyLiveDeltaSink sink) { this.sink = sink; }
        public int WritebackOrder => 90;
        public void WriteBackFromWorld() => sink.FlushStructures();
    }
}
