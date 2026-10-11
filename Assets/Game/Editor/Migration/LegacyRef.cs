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

    /// <summary>JSON converter for LegacyRef (null or an object with $ref, name, type and optional sub). MIG-002 implements it.</summary>
    public sealed class LegacyRefConverter : Newtonsoft.Json.JsonConverter<LegacyRef>
    {
        public override LegacyRef ReadJson(Newtonsoft.Json.JsonReader reader, System.Type objectType, LegacyRef existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer) { throw new System.NotImplementedException("MIG-002"); }
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, LegacyRef value, Newtonsoft.Json.JsonSerializer serializer) { throw new System.NotImplementedException("MIG-002"); }
    }
}
