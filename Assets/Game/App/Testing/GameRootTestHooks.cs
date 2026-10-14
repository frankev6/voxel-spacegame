#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.Testing
{
    /// <summary>Boot-time overrides for tests (SPEC-71 decision 8, §4.4). Read once by GameRoot.Awake. Editor and development builds only.
    /// Created by FND-005 ahead of TST-003, with the SPEC-71 shape (StaticStateOk sits on the fields: the attribute is field-only).</summary>
    public static class GameRootTestHooks
    {
        [Game.Core.StaticStateOk("test hooks, reset by GameFlowFixture")] public static string SaveRoot;          // null = default
        [Game.Core.StaticStateOk("test hooks, reset by GameFlowFixture")] public static string SettingsPath;      // null = default
        [Game.Core.StaticStateOk("test hooks, reset by GameFlowFixture")] public static bool SkipIntroScreens;    // MainMenu splash skipped
        [Game.Core.StaticStateOk("test hooks, reset by GameFlowFixture")] public static bool SkipCampaignOpening; // no automatic story beats (QA-001)

        public static void Reset() { SaveRoot = null; SettingsPath = null; SkipIntroScreens = false; SkipCampaignOpening = false; }
    }
}
#endif
