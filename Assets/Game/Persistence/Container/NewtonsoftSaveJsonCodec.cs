using System;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5 with the settings of §5.4 step 10. Used for META, meta.json, profile.json and participant JSON.</summary>
    public sealed class NewtonsoftSaveJsonCodec : Game.Core.Save.ISaveJsonCodec
    {
        /// <summary>A new settings object per call: Formatting.None, MissingMemberHandling.Error, NullValueHandling.Include, UTC dates as
        /// "yyyy-MM-ddTHH:mm:ss.fffZ", invariant culture, enums as names, no type names.</summary>
        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
            Culture = CultureInfo.InvariantCulture,
            TypeNameHandling = TypeNameHandling.None,
            Converters = { new StringEnumConverter() },
        };

        readonly JsonSerializerSettings settings = Settings;

        public byte[] Serialize<T>(T value) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, settings));

        /// <summary>Throws Game.Core.Save.SaveFormatException for malformed JSON or an undeclared property.</summary>
        public T Deserialize<T>(ReadOnlySpan<byte> utf8)
        {
            try { return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(utf8), settings); }
            catch (JsonException e) { throw new Game.Core.Save.SaveFormatException("Invalid JSON: " + e.Message, -1); }
        }
    }
}
