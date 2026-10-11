namespace Game.Voxel.PhysicsBody
{
    /// <summary>Deterministic collider update windows (SPEC-12 §5.5).</summary>
    public static class ColliderInstallSchedule
    {
        public static bool IsUrgent(ColliderPriority p) => p <= ColliderPriority.Split;
        /// <summary>Urgent requests install at a bucket boundary; edit/spawn install when built.</summary>
        public static uint InstallTick(ColliderPriority p, uint requestTick, int delay, int bucket)
            => IsUrgent(p) ? RoundUp(requestTick + (uint)delay, (uint)bucket) : 0;
        /// <summary>True after the last request tick that can belong to this window.</summary>
        public static bool IsWindowClosed(uint installTick, uint currentTick, int delay)
            => installTick == 0 || currentTick > installTick - (uint)delay;
        public static uint RoundUp(uint value, uint multiple) => ((value + multiple - 1) / multiple) * multiple;
    }
}
