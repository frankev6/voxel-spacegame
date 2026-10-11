using UnityEngine;
namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §3.8: convert linear settings to mixer decibels.</summary>
    public static class VolumeMath
    {
        public static float ToDb(float linear) => linear <= .0001f ? -80f : 20f * Mathf.Log10(linear);
    }
}
