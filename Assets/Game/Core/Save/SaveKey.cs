namespace Game.Core.Save
{
    /// <summary>Four-character participant keys and their FourCC form (SPEC-02 §4.7).</summary>
    public static class SaveKey
    {
        /// <summary>Non-null, Length == 4, every char in 'A'..'Z' or '0'..'9'.</summary>
        public static bool IsValid(string key)
        {
            if (key == null || key.Length != 4) return false;
            foreach (char c in key) if (!IsKeyChar(c)) return false;
            return true;
        }

        /// <summary>ArgumentException if !IsValid; c0 | c1&lt;&lt;8 | c2&lt;&lt;16 | c3&lt;&lt;24.</summary>
        public static uint ToFourCC(string key)
        {
            if (!IsValid(key)) throw new System.ArgumentException($"'{key}' is not a save key (4 characters A-Z, 0-9)", nameof(key));
            return key[0] | (uint)key[1] << 8 | (uint)key[2] << 16 | (uint)key[3] << 24;
        }

        /// <summary>Inverse of ToFourCC; SaveFormatException if a byte is not [A-Z0-9].</summary>
        public static string FromFourCC(uint fourCC)
        {
            var chars = new char[4];
            for (int i = 0; i < 4; i++)
            {
                char c = (char)((fourCC >> (8 * i)) & 0xFF);
                if (!IsKeyChar(c)) throw new SaveFormatException($"invalid save key byte 0x{(int)c:X2}", -1);
                chars[i] = c;
            }
            return new string(chars);
        }

        static bool IsKeyChar(char c) => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }
}
