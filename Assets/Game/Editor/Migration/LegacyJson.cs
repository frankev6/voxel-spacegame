namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Strict legacy JSON reading.</summary>
    public static class LegacyJson
    {
        public static Newtonsoft.Json.JsonSerializerSettings StrictSettings => throw new System.NotImplementedException("MIG-002");      // MissingMemberHandling.Error, FloatParseHandling.Double, custom LegacyRef converter
        public static T Read<T>(LegacyFile file, Game.Data.Validation.ValidationReport report) where T : Game.Editor.Migration.Dto.LegacyDto { throw new System.NotImplementedException("MIG-002"); } // null + MIG-E002 on failure
        public static T ReadFile<T>(string legacyRoot, string relativePath, Game.Data.Validation.ValidationReport report) where T : class { throw new System.NotImplementedException("MIG-002"); } // for files without $path (narrative/*.json)
    }
}
