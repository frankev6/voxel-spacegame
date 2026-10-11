namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. A legacy object reference {"$ref","name","type","sub"} or null. Read through LegacyRefConverter.</summary>
    [Newtonsoft.Json.JsonConverter(typeof(LegacyRefConverter))]
    public readonly struct LegacyRef
    {
        public readonly string Path; public readonly string Name; public readonly string Type; public readonly string Sub;
        public LegacyRef(string path, string name, string type, string sub) { Path = path; Name = name; Type = type; Sub = sub; }
        public bool IsNull => Path == null;
    }

    /// <summary>JSON converter for LegacyRef: null, or an object with "$ref", "name", "type" and optional "sub" (any other property is an error).</summary>
    public sealed class LegacyRefConverter : Newtonsoft.Json.JsonConverter<LegacyRef>
    {
        public override LegacyRef ReadJson(Newtonsoft.Json.JsonReader reader, System.Type objectType, LegacyRef existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (reader.TokenType == Newtonsoft.Json.JsonToken.Null) return default;
            if (reader.TokenType != Newtonsoft.Json.JsonToken.StartObject)
                throw Error(reader, reader.Path, $"Expected a reference object or null, found {reader.TokenType}.");
            string path = null, name = null, type = null, sub = null;
            while (reader.Read() && reader.TokenType != Newtonsoft.Json.JsonToken.EndObject)
            {
                string property = (string)reader.Value;
                string at = reader.Path;
                reader.Read();
                string value = reader.TokenType == Newtonsoft.Json.JsonToken.Null ? null : System.Convert.ToString(reader.Value, System.Globalization.CultureInfo.InvariantCulture);
                switch (property)
                {
                    case "$ref": path = value; break;
                    case "name": name = value; break;
                    case "type": type = value; break;
                    case "sub": sub = value; break;
                    default: throw Error(reader, at, $"Could not find member '{property}' on a legacy reference.");
                }
            }
            if (path == null) throw Error(reader, reader.Path, "A legacy reference has no $ref.");
            return new LegacyRef(path, name, type, sub);
        }

        // Carries the JSON path in Exception.Path (MIG-E002 reports it as the field).
        static Newtonsoft.Json.JsonSerializationException Error(Newtonsoft.Json.JsonReader reader, string path, string message)
        {
            var line = reader as Newtonsoft.Json.IJsonLineInfo;
            return new Newtonsoft.Json.JsonSerializationException($"{message} Path '{path}'.", path, line?.LineNumber ?? 0, line?.LinePosition ?? 0, null);
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, LegacyRef value, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (value.IsNull) { writer.WriteNull(); return; }
            writer.WriteStartObject();
            writer.WritePropertyName("$ref"); writer.WriteValue(value.Path);
            writer.WritePropertyName("name"); writer.WriteValue(value.Name);
            writer.WritePropertyName("type"); writer.WriteValue(value.Type);
            if (value.Sub != null) { writer.WritePropertyName("sub"); writer.WriteValue(value.Sub); }
            writer.WriteEndObject();
        }
    }
}
