using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    [ContentIdPrefix("config_")]
    public sealed class VoxelPaletteDefinition : ContentDefinition
    {
        [SerializeField] private Color32[] colors = new Color32[256];
        [SerializeField] private string[] names = new string[256];
        public System.Collections.Generic.IReadOnlyList<Color32> Colors => colors;
        public System.Collections.Generic.IReadOnlyList<string> Names => names;
    }
}
