namespace Game.Data.Definitions
{
    /// <summary>Allowed ID prefixes for a definition type. Inherited; the nearest declaration wins. Implements SPEC-04 §4.3.</summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ContentIdPrefixAttribute : System.Attribute
    {
        public ContentIdPrefixAttribute(params string[] prefixes) { Prefixes = prefixes; }
        public string[] Prefixes { get; }                 // each ends with '_'
    }
}
