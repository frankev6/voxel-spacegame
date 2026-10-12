using System;
using UnityEngine;

namespace Game.Editor.Audio
{
    [Serializable]
    public sealed class AudioProvenanceEntry
    {
        public AudioClip clip;
        public string source;
        public string licence;
        public ProvenanceStatus status;
        public string note;
    }
}
