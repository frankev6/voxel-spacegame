using UnityEngine;
using Game.Data.Validation;

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
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (colors == null || colors.Length != 256)
                scope.Error("REN-E002", nameof(colors), "Paint palette must contain exactly 256 colors.");
        }
    }
}
