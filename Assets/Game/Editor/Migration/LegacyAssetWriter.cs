using System;
using System.Collections.Generic;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Creates or updates content assets, respecting hand edits through the manifest.</summary>
    public sealed class LegacyAssetWriter
    {
        public LegacyAssetWriter(string contentRoot, MigrationManifest manifest, bool force, Game.Data.Validation.ValidationReport report) { throw new System.NotImplementedException("MIG-004"); }
        public T CreateOrUpdate<T>(string area, string id, Action<UnityEditor.SerializedObject> fill, out WriteOutcome outcome)
            where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("MIG-004"); }          // area = folder under Assets/Game/Content
        public bool SetLinkIfEmpty(Game.Data.Definitions.ContentDefinition asset, string propertyPath, UnityEngine.Object value) { throw new System.NotImplementedException("MIG-004"); }
        public bool SetLinkArrayIfEmpty(Game.Data.Definitions.ContentDefinition asset, string propertyPath, IReadOnlyList<UnityEngine.Object> values) { throw new System.NotImplementedException("MIG-004"); }
        public IReadOnlyList<(string AssetPath, WriteOutcome Outcome)> Outcomes => throw new System.NotImplementedException("MIG-004");
    }
}
