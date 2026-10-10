namespace Game.Core.Save
{
    /// <summary>Four-character participant keys and their FourCC form (SPEC-02 §4.7).</summary>
    public static class SaveKey
    {
        public static bool IsValid(string key) { throw new System.NotImplementedException("CORE-007"); }              // non-null, Length == 4, every char in 'A'..'Z' or '0'..'9'
        public static uint ToFourCC(string key) { throw new System.NotImplementedException("CORE-007"); }             // ArgumentException if !IsValid; c0 | c1<<8 | c2<<16 | c3<<24
        public static string FromFourCC(uint fourCC) { throw new System.NotImplementedException("CORE-007"); }        // inverse; SaveFormatException if a byte is not [A-Z0-9]
    }
}
