using System;
using System.Text;

namespace Game.Editor.Narrative.Dialog
{
    /// <summary>SPEC-41 §4.4 exact text escaping. Structural parsing follows in DLG-002.</summary>
    public static partial class DialogScriptParser
    {
        public static string Escape(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' || c == '#' || c == '{' || c == '}' || c == '[' || c == ']' ||
                    c == '<' || c == '>' || c == ':' || c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                    result.Append('\\');
                result.Append(c);
            }
            return result.ToString();
        }
        public static string Unescape(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    if (++i == text.Length) throw new FormatException("Unterminated dialog escape.");
                    c = text[i];
                    if (c != '\\' && c != '#' && c != '{' && c != '}' && c != '[' && c != ']' &&
                        c != '<' && c != '>' && c != '/' && c != ':')
                        throw new FormatException("Unsupported dialog escape: \\" + c);
                }
                result.Append(c);
            }
            return result.ToString();
        }
        // TODO(DLG-002): implement Parse(path, text), structural diagnostics and the seven required cases.
    }
}
