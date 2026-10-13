using System;
using Game.Core;
using Game.Data.Definitions;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.5: pure state machine, bounded signal history and simulated director time; no Unity calls.</summary>
    internal sealed class MusicDirector
    {
        readonly ContentHash menu, exploration, combat, editor;
        readonly double window, hold;
        readonly int enterSignals;
        readonly float fadeDefault, fadeToCombat, fadeFromCombat;
        readonly double[] signals = new double[32];
        double time, lastSignal = double.NegativeInfinity;
        int next, count;
        bool inCombat;
        MusicContext previousContext = MusicContext.Silent;
        internal MusicState State { get; private set; } = MusicState.Silent;

        internal MusicDirector(ContentHash menu, ContentHash exploration, ContentHash combat, ContentHash editor,
            float windowSeconds = 2, int enterSignals = 3, float holdSeconds = 8,
            float fadeDefault = 2, float fadeToCombat = 1, float fadeFromCombat = 4)
        {
            this.menu = menu; this.exploration = exploration; this.combat = combat; this.editor = editor;
            window = Duration(windowSeconds, nameof(windowSeconds)); hold = Duration(holdSeconds, nameof(holdSeconds));
            if (enterSignals < 1) throw new ArgumentOutOfRangeException(nameof(enterSignals));
            this.enterSignals = enterSignals;
            this.fadeDefault = Duration(fadeDefault, nameof(fadeDefault));
            this.fadeToCombat = Duration(fadeToCombat, nameof(fadeToCombat));
            this.fadeFromCombat = Duration(fadeFromCombat, nameof(fadeFromCombat));
        }
        static float Duration(float value, string name)
        { if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name); return value; }

        internal MusicCommand Update(MusicContext context, int combatSignals, bool hasOverride,
            ContentHash overrideCue, float overrideFade, float dt)
        {
            if (float.IsFinite(dt) && dt > 0) time += dt;
            if (context != previousContext) { ClearSignals(); previousContext = context; }
            if (context == MusicContext.World)
            {
                if (combatSignals > 0)
                {
                    // All signals of this frame share a timestamp. More than 32 replaces the entire bounded history.
                    int added = Math.Min(combatSignals, signals.Length);
                    for (int i = 0; i < added; i++)
                    { signals[next] = time; next = (next + 1) % signals.Length; count = Math.Min(count + 1, signals.Length); }
                    lastSignal = time;
                }
                int recent = 0;
                for (int i = 0; i < count; i++) if (time - signals[i] <= window) recent++;
                if (!inCombat && recent >= enterSignals) inCombat = true;
                else if (inCombat && time - lastSignal >= hold) inCombat = false;
            }

            MusicState target;
            if (context == MusicContext.Silent) target = MusicState.Silent;
            else if (hasOverride) target = MusicState.Override;
            else if (context == MusicContext.Menu) target = MusicState.Menu;
            else if (context == MusicContext.Editor) target = MusicState.Editor;
            else if (context == MusicContext.World) target = inCombat ? MusicState.Combat : MusicState.Exploration;
            else target = MusicState.Silent;

            ContentHash cue = target == MusicState.Menu ? menu : target == MusicState.Exploration ? exploration :
                target == MusicState.Combat ? combat : target == MusicState.Editor ? editor :
                target == MusicState.Override ? overrideCue : ContentHash.None;
            float fade = target == MusicState.Override ? overrideFade : target == MusicState.Combat ? fadeToCombat :
                State == MusicState.Combat && target == MusicState.Exploration ? fadeFromCombat : fadeDefault;
            var command = new MusicCommand { Change = target != State, Target = target, Cue = cue, Fade = fade };
            State = target; return command;
        }
        void ClearSignals() { next = count = 0; inCombat = false; lastSignal = double.NegativeInfinity; }
        internal void Reset()
        { time = 0; ClearSignals(); State = MusicState.Silent; previousContext = MusicContext.Silent; }
    }
}
