using Game.Data.Validation;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Data.Definitions
{
    /// <summary>Audio routing, music and combat-cue configuration (SPEC-64 §4.3). Exactly one instance:
    /// Assets/Game/Content/Audio/config_audio.asset (id "config_audio").</summary>
    [ContentIdPrefix("config_")]
    [CreateAssetMenu(menuName = "Game/Content/Config/Audio Config", fileName = "config_audio")]
    public sealed class AudioConfigDefinition : ContentDefinition
    {
        public const int UiSoundCount = 11;   // values of Game.Core.Audio.UiSound

        [SerializeField] AudioMixer mixer;                              // Assets/Game/Audio/Mixer/GameMixer.mixer
        [SerializeField] AudioMixerGroup sfxGroup, uiGroup, musicGroup, ambienceGroup;
        [SerializeField] AudioCueDefinition[] uiSounds = new AudioCueDefinition[UiSoundCount];   // index = (int)UiSound
        [SerializeField] AudioCueDefinition musicMenu, musicExploration, musicCombat, musicEditor, ambienceWorld;
        [SerializeField] AudioCueDefinition impactKinetic, impactEnergy, impactExplosive, impactRam,
                                            explosionSmall, explosionLarge, shieldHit, cellBreak, breakOff, shipDeath;
        [SerializeField] AudioCueDefinition engineLoop, beamLoop;       // may be null: that loop stays silent
        [SerializeField] SceneMusicEntry[] sceneMusic = {
            new SceneMusicEntry { sceneName = "Boot", context = MusicContext.Silent },
            new SceneMusicEntry { sceneName = "MainMenu", context = MusicContext.Menu },
            new SceneMusicEntry { sceneName = "World", context = MusicContext.World },
            new SceneMusicEntry { sceneName = "BlueprintLab", context = MusicContext.Editor } };
        [SerializeField] float combatRadius = 500f, combatWindowSeconds = 2f, combatHoldSeconds = 8f;
        [SerializeField] int combatEnterSignals = 3;
        [SerializeField] float fadeDefault = 2f, fadeToCombat = 1f, fadeFromCombat = 4f;
        [SerializeField] float stingerDuckDb = -8f, stingerAttack = 0.2f, stingerRelease = 1f;
        [SerializeField] float loopFadeSeconds = 0.1f, beamLoopRadius = 300f;

        public AudioMixer Mixer => mixer;
        public AudioMixerGroup SfxGroup => sfxGroup; public AudioMixerGroup UiGroup => uiGroup;
        public AudioMixerGroup MusicGroup => musicGroup; public AudioMixerGroup AmbienceGroup => ambienceGroup;
        public System.Collections.Generic.IReadOnlyList<AudioCueDefinition> UiSounds => uiSounds;
        public AudioCueDefinition MusicMenu => musicMenu; public AudioCueDefinition MusicExploration => musicExploration;
        public AudioCueDefinition MusicCombat => musicCombat; public AudioCueDefinition MusicEditor => musicEditor;
        public AudioCueDefinition AmbienceWorld => ambienceWorld;
        public AudioCueDefinition ImpactKinetic => impactKinetic; public AudioCueDefinition ImpactEnergy => impactEnergy;
        public AudioCueDefinition ImpactExplosive => impactExplosive; public AudioCueDefinition ImpactRam => impactRam;
        public AudioCueDefinition ExplosionSmall => explosionSmall; public AudioCueDefinition ExplosionLarge => explosionLarge;
        public AudioCueDefinition ShieldHit => shieldHit; public AudioCueDefinition CellBreak => cellBreak;
        public AudioCueDefinition BreakOff => breakOff; public AudioCueDefinition ShipDeath => shipDeath;
        public AudioCueDefinition EngineLoop => engineLoop; public AudioCueDefinition BeamLoop => beamLoop;
        public System.Collections.Generic.IReadOnlyList<SceneMusicEntry> SceneMusic => sceneMusic;
        public float CombatRadius => combatRadius; public float CombatWindowSeconds => combatWindowSeconds; public float CombatHoldSeconds => combatHoldSeconds;
        public int CombatEnterSignals => combatEnterSignals;
        public float FadeDefault => fadeDefault; public float FadeToCombat => fadeToCombat; public float FadeFromCombat => fadeFromCombat;
        public float StingerDuckDb => stingerDuckDb; public float StingerAttack => stingerAttack; public float StingerRelease => stingerRelease;
        public float LoopFadeSeconds => loopFadeSeconds; public float BeamLoopRadius => beamLoopRadius;

        /// <summary>AUD-E010 mixer or a group missing; AUD-E011 uiSounds not 11 entries or one missing; AUD-E012 a music cue missing, not on the
        /// Music bus or not looping; AUD-E013 ambienceWorld set but not an Ambience loop; AUD-E014 a combat cue missing; AUD-E015
        /// combatEnterSignals &lt; 1; AUD-E016 sceneMusic lacks one of Boot, MainMenu, World, BlueprintLab (SPEC-64 open question 12).</summary>
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (mixer == null) scope.Error("AUD-E010", nameof(mixer), "The mixer is not set.");
            if (sfxGroup == null) scope.Error("AUD-E010", nameof(sfxGroup), "The Sfx mixer group is not set.");
            if (uiGroup == null) scope.Error("AUD-E010", nameof(uiGroup), "The Ui mixer group is not set.");
            if (musicGroup == null) scope.Error("AUD-E010", nameof(musicGroup), "The Music mixer group is not set.");
            if (ambienceGroup == null) scope.Error("AUD-E010", nameof(ambienceGroup), "The Ambience mixer group is not set.");
            if (uiSounds == null || uiSounds.Length != UiSoundCount)
                scope.Error("AUD-E011", nameof(uiSounds), $"UI sounds need exactly {UiSoundCount} entries (one per UiSound), not {uiSounds?.Length ?? 0}.");
            else for (int i = 0; i < uiSounds.Length; i++) if (uiSounds[i] == null) scope.Error("AUD-E011", $"{nameof(uiSounds)}[{i}]", "UI sound is not set.");
            Music(scope, musicMenu, nameof(musicMenu)); Music(scope, musicExploration, nameof(musicExploration));
            Music(scope, musicCombat, nameof(musicCombat)); Music(scope, musicEditor, nameof(musicEditor));
            if (ambienceWorld != null && (ambienceWorld.Bus != AudioBus.Ambience || !ambienceWorld.Loop))
                scope.Error("AUD-E013", nameof(ambienceWorld), "The world ambience must be a looping cue on the Ambience bus.");
            Combat(scope, impactKinetic, nameof(impactKinetic)); Combat(scope, impactEnergy, nameof(impactEnergy));
            Combat(scope, impactExplosive, nameof(impactExplosive)); Combat(scope, impactRam, nameof(impactRam));
            Combat(scope, explosionSmall, nameof(explosionSmall)); Combat(scope, explosionLarge, nameof(explosionLarge));
            Combat(scope, shieldHit, nameof(shieldHit)); Combat(scope, cellBreak, nameof(cellBreak));
            Combat(scope, breakOff, nameof(breakOff)); Combat(scope, shipDeath, nameof(shipDeath));
            if (combatEnterSignals < 1) scope.Error("AUD-E015", nameof(combatEnterSignals), $"Combat needs at least 1 signal to start, not {combatEnterSignals}.");
            foreach (var scene in new[] { "Boot", "MainMenu", "World", "BlueprintLab" })
                if (sceneMusic == null || System.Array.FindIndex(sceneMusic, e => e.sceneName == scene) < 0)
                    scope.Error("AUD-E016", nameof(sceneMusic), $"Scene music has no entry for {scene}.");
        }

        static void Music(ValidationScope scope, AudioCueDefinition cue, string field)
        {
            if (cue == null) scope.Error("AUD-E012", field, "Music cue is not set.");
            else if (cue.Bus != AudioBus.Music || !cue.Loop) scope.Error("AUD-E012", field, $"Music cue {cue.IdString} must loop on the Music bus.");
        }

        static void Combat(ValidationScope scope, AudioCueDefinition cue, string field)
        { if (cue == null) scope.Error("AUD-E014", field, "Combat cue is not set."); }
    }
}
