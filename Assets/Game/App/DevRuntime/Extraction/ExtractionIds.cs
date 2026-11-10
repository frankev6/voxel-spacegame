#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Extraction
{
    /// <summary>Blueprint ids made by Game/Content/Build Lantern and Den (Assets/Game/Editor/Content/LanternBuilder.cs, DenBuilder.cs).</summary>
    internal static class ExtractionIds
    {
        internal const string Core = "ship_lantern_core", Post = "ship_lantern_post", Tank = "ship_lantern_tank_";
        internal const string Rock = "ship_den_rock", Shard = "ship_den_shard", Gun = "ship_hollow_gun_light";
    }
}
#endif
