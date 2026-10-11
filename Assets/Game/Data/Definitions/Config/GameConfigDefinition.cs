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

        /// <summary>FND-E001 (no start blueprint), FND-E002 (a number outside its range).</summary>
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        { throw new System.NotImplementedException("FND-005"); }
    }
}
