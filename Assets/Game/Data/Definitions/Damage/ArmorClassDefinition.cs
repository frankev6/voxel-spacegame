using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-13 section 4.7: contiguous zero-based armor class referenced by block content.</summary>
    [ContentIdPrefix("armor_")]
    [CreateAssetMenu(menuName = "Game/Content/Armor Class", fileName = "armor_")]
    public sealed class ArmorClassDefinition : ContentDefinition
    {
        [SerializeField] private byte index;
        [SerializeField] private string displayName;
        public byte Index => index;
        public string DisplayName => displayName;
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (context?.Catalog == null) return;
            var classes = context.Catalog.All<ArmorClassDefinition>();
            if (index >= classes.Count) scope.Error("DMG-E004", nameof(index), "Armor indices must be contiguous from zero.");
            foreach (var other in classes)
                if (other != this && other.index == index) { scope.Error("DMG-E004", nameof(index), "Duplicate armor index."); break; }
        }
    }
}
