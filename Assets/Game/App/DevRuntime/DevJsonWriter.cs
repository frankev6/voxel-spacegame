#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Text;

namespace Game.App.DevRuntime
{
    /// <summary>JSON scalar encoding for dev results, without a runtime JSON dependency (SPEC-70 §3.7).</summary>
    public static class DevJsonWriter
    {
        public static string Quote(string value)
        {
            if (value == null) return "null";
            var text = new StringBuilder(value.Length + 2).Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < 32 || char.IsSurrogate(c)) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else text.Append(c);
                        break;
                }
            }
            return text.Append('"').ToString();
        }
        public static string Number(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "null" : value.ToString("R", CultureInfo.InvariantCulture);
    }
}
#endif
