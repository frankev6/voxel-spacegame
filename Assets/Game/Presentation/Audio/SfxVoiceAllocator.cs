using Unity.Collections;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.3 step 6: allocation-free selection from the fixed SFX voice pool.</summary>
    internal static class SfxVoiceAllocator
    {
        internal static int Allocate(NativeArray<SfxCandidate> candidates, AudioService service,
            NativeHashMap<ulong, double> lastStart, double now, int maxNew)
        {
            int started = 0;
            for (int i = 0; i < candidates.Length && started < maxNew; i++)
            {
                var candidate = candidates[i];
                if (!service.TryStartSfx(candidate, now)) continue;
                lastStart[candidate.GroupKey] = now; started++;
            }
            return started;
        }
        internal static int Select(in SfxCandidate candidate, VoiceInfo[] voices, in SfxCueInfo cue, out bool stolen)
        {
            int same = 0, oldest = -1, free = -1, victim = -1;
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                if (!voice.Active) { if (free < 0) free = i; continue; }
                if (voice.Slot == candidate.Slot)
                { same++; if (oldest < 0 || voice.StartTime < voices[oldest].StartTime) oldest = i; }
                if (victim < 0 || voice.Priority > voices[victim].Priority ||
                    (voice.Priority == voices[victim].Priority && (voice.Audibility < voices[victim].Audibility ||
                    (voice.Audibility == voices[victim].Audibility && voice.StartTime < voices[victim].StartTime)))) victim = i;
            }
            stolen = false;
            if (same >= cue.MaxVoices)
            {
                if (oldest < 0 || candidate.Audibility <= voices[oldest].Audibility) return -1;
                stolen = true; return oldest;
            }
            if (free >= 0) return free;
            if (victim < 0 || voices[victim].Priority < cue.Priority ||
                (voices[victim].Priority == cue.Priority && voices[victim].Audibility >= candidate.Audibility)) return -1;
            stolen = true; return victim;
        }
    }
}
