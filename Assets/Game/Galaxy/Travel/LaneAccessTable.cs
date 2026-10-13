namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.7 staged lane cache; TRV-003 owns watchers, revisions and disposal.</summary>
    internal sealed class LaneAccessTable : System.IDisposable
    {
        public uint Revision = 0;
        public int BuiltForGeneration = -1;
        public bool[] GateOpen = null;
        public Game.Data.Logic.LogicWatcher[] Watchers = null;
        public bool PendingContext = false;
        public void Dispose() => throw new System.NotImplementedException("TRV-003");
    }
}
