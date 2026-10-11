using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Strict legacy JSON reading (decision 3): an undeclared property is MIG-E002 with its JSON path.</summary>
    public static class LegacyJson
    {
        /// <summary>A new settings object per call (callers may not share mutable settings): MissingMemberHandling.Error, NullValueHandling.Include,
        /// FloatParseHandling.Double, DateParseHandling.None, metadata ($path, $type, $ref) read as ordinary properties, and the LegacyRef converter.</summary>
        public static JsonSerializerSettings StrictSettings => new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            FloatParseHandling = FloatParseHandling.Double,
            DateParseHandling = DateParseHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            Converters = { new LegacyRefConverter() },
        };

        /// <summary>Reads an indexed file into its DTO; null + MIG-E002 (file path, JSON path) on failure.</summary>
        public static T Read<T>(LegacyFile file, Game.Data.Validation.ValidationReport report) where T : Game.Editor.Migration.Dto.LegacyDto
        {
            try { return file.Json.ToObject<T>(JsonSerializer.Create(StrictSettings)); }
            catch (JsonException e) { ReportError(report, file.RelativePath, e); return null; }
        }

        /// <summary>For files without $path (narrative/*.json); null + MIG-E002 on failure.</summary>
        public static T ReadFile<T>(string legacyRoot, string relativePath, Game.Data.Validation.ValidationReport report) where T : class
        {
            string full = Path.Combine(legacyRoot, relativePath);
            if (!File.Exists(full)) { report.Error("MIG-E002", relativePath, "", "The file does not exist."); return null; }
            try
            {
                using var text = new StringReader(File.ReadAllText(full));
                using var reader = new JsonTextReader(text) { FloatParseHandling = FloatParseHandling.Double, DateParseHandling = DateParseHandling.None };
                var result = JsonSerializer.Create(StrictSettings).Deserialize<T>(reader);
                if (result == null) report.Error("MIG-E002", relativePath, "", "The file holds no JSON object.");
                return result;
            }
            catch (JsonException e) { ReportError(report, relativePath, e); return null; }
        }

        /// <summary>Parses one JSON object with the strict number and date rules (used by LegacyContentIndex).</summary>
        internal static JObject ParseObject(string text)
        {
            using var reader = new JsonTextReader(new StringReader(text)) { FloatParseHandling = FloatParseHandling.Double, DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }

        static void ReportError(Game.Data.Validation.ValidationReport report, string path, JsonException e)
        {
            string jsonPath = e is JsonSerializationException s ? s.Path : e is JsonReaderException r ? r.Path : "";
            report.Error("MIG-E002", path, jsonPath ?? "", e.Message);
        }
    }
}
