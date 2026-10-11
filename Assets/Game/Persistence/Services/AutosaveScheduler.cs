using Game.Core;
using Game.Persistence.Data;

namespace Game.Persistence.Services
{
    /// <summary>Timer, scene-transition and star-system autosaves (SPEC-05 §5.9). Update runs every frame and allocates nothing; it starts a
    /// save only when one is pending, allowed and the service is idle. Implements SPEC-05 §4.5.</summary>
    public sealed class AutosaveScheduler
    {
        readonly SaveService service;
        readonly SaveServiceConfig config;
        readonly IPauseService pause;
        float elapsed;                                   // unpaused gameplay seconds since the last save
        float sinceLastSave = float.PositiveInfinity;    // real seconds since the last save (none yet: no spacing applies)
        float retryTimer;
        bool hasPending;
        AutosaveTrigger pending;

        internal AutosaveScheduler(SaveService service, SaveServiceConfig config, IPauseService pause)
        {
            this.service = service; this.config = config; this.pause = pause;
        }

        /// <summary>Called once per frame by Game.App (SPEC-01) with Time.unscaledDeltaTime.</summary>
        public void Update(float unscaledDeltaTime)
        {
            // 1.
            if (!service.HasActiveSessionContext) { hasPending = false; elapsed = 0f; retryTimer = 0f; return; }
            // 2.
            if (pause == null || !pause.IsPaused) elapsed += unscaledDeltaTime;
            sinceLastSave += unscaledDeltaTime;
            // 3.
            if (elapsed >= config.AutosaveIntervalSeconds && !hasPending) { hasPending = true; pending = AutosaveTrigger.Timer; }
            if (!hasPending) return;
            // 4.
            if (retryTimer > 0f) { retryTimer -= unscaledDeltaTime; return; }
            // 5.
            if (service.IsBusy) return;
            if (!service.CanSave(out _) || service.ActiveProfile.Value == null) { retryTimer = config.BlockedRetrySeconds; return; }
            var reason = ReasonFor(pending);
            hasPending = false;
            _ = service.SaveAsync(service.NextAutosaveSlot(service.ActiveProfile), reason);
        }

        /// <summary>Timer and StarSystemEntered are ignored within AutosaveMinSpacingSeconds of the last save; SceneTransition always sets
        /// the pending request (and overrides a pending Timer); Quit belongs to the quit path.</summary>
        public void Request(AutosaveTrigger trigger)
        {
            switch (trigger)
            {
                case AutosaveTrigger.SceneTransition:
                    hasPending = true; pending = trigger; retryTimer = 0f;
                    break;
                case AutosaveTrigger.Timer:
                case AutosaveTrigger.StarSystemEntered:
                    if (sinceLastSave < config.AutosaveMinSpacingSeconds) return;
                    if (hasPending && pending == AutosaveTrigger.SceneTransition) return;
                    hasPending = true; pending = trigger;
                    break;
            }
        }

        /// <summary>Called by SaveService after any successful save and after a load (restarts the timer).</summary>
        public void NotifySaved() { elapsed = 0f; sinceLastSave = 0f; }

        /// <summary>Diagnostics and HUD.</summary>
        public float SecondsUntilTimerSave => System.Math.Max(0f, config.AutosaveIntervalSeconds - elapsed);
        public bool HasPendingRequest => hasPending;

        static Game.Core.Save.SaveReason ReasonFor(AutosaveTrigger t)
            => t == AutosaveTrigger.SceneTransition ? Game.Core.Save.SaveReason.AutoSceneTransition
             : t == AutosaveTrigger.StarSystemEntered ? Game.Core.Save.SaveReason.AutoStarSystemEntered
             : Game.Core.Save.SaveReason.AutoTimer;
    }
}
