using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Boot and session settings; exactly one asset, config_game. Implements SPEC-01 §4.2.</summary>
    [ContentIdPrefix("config_")]
    [CreateAssetMenu(menuName = "Game/Content/Config/Game Config", fileName = "config_game")]
    public sealed class GameConfigDefinition : ContentDefinition
    {
        [SerializeField] private ShipBlueprintDefinition startBlueprint;     // player flagship for a new game
        [SerializeField] private string defaultCommanderName = "Commander";
        [SerializeField] private ulong devQuickStartSeed = 12345ul;
        [SerializeField] private float sceneReadyTimeoutSeconds = 30f;       // 5..120
        [SerializeField] private int persistentIdBlockSize = 4096;           // 256..65536
        [SerializeField] private int persistentIdRefillThreshold = 1024;     // < block size
        [SerializeField] private float defeatDelaySeconds = 3f;              // unscaled; 0..10
        [SerializeField] private float combatDamageWindowSeconds = 10f;      // SPEC-05 Q14
        [SerializeField] private float settingsSaveDebounceSeconds = 0.5f;

        public ShipBlueprintDefinition StartBlueprint => startBlueprint;
        public string DefaultCommanderName => defaultCommanderName;
        public ulong DevQuickStartSeed => devQuickStartSeed;
        public float SceneReadyTimeoutSeconds => sceneReadyTimeoutSeconds;
        public int PersistentIdBlockSize => persistentIdBlockSize;
        public int PersistentIdRefillThreshold => persistentIdRefillThreshold;
        public float DefeatDelaySeconds => defeatDelaySeconds;
        public float CombatDamageWindowSeconds => combatDamageWindowSeconds;
        public float SettingsSaveDebounceSeconds => settingsSaveDebounceSeconds;

        /// <summary>FND-E002 (a number outside its range). FND-E001 (no start blueprint) is a warning here: no ship blueprint exists before
        /// VOX-010/MIG-008, and boot must not fail for it. GameConfigValidator makes it an error once any blueprint exists (SPEC-01 open
        /// question 8).</summary>
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (startBlueprint == null)
                scope.Warning("FND-E001", nameof(startBlueprint), "No start blueprint: New Game is unavailable until one is set.");
            void Range(bool ok, string field, string message) { if (!ok) scope.Error("FND-E002", field, message); }
            Range(sceneReadyTimeoutSeconds >= 5f && sceneReadyTimeoutSeconds <= 120f, nameof(sceneReadyTimeoutSeconds), $"Scene ready timeout {sceneReadyTimeoutSeconds} s is outside 5..120.");
            Range(persistentIdBlockSize >= 256 && persistentIdBlockSize <= 65536, nameof(persistentIdBlockSize), $"Persistent id block size {persistentIdBlockSize} is outside 256..65536.");
            Range(persistentIdRefillThreshold >= 0 && persistentIdRefillThreshold < persistentIdBlockSize, nameof(persistentIdRefillThreshold), $"Refill threshold {persistentIdRefillThreshold} must be at least 0 and below the block size {persistentIdBlockSize}.");
            Range(defeatDelaySeconds >= 0f && defeatDelaySeconds <= 10f, nameof(defeatDelaySeconds), $"Defeat delay {defeatDelaySeconds} s is outside 0..10.");
            Range(combatDamageWindowSeconds > 0f, nameof(combatDamageWindowSeconds), $"Combat damage window {combatDamageWindowSeconds} s must be greater than 0.");
            Range(settingsSaveDebounceSeconds >= 0f, nameof(settingsSaveDebounceSeconds), $"Settings save debounce {settingsSaveDebounceSeconds} s must not be negative.");
            Range(!string.IsNullOrWhiteSpace(defaultCommanderName), nameof(defaultCommanderName), "The default commander name is empty.");
        }
    }
}
