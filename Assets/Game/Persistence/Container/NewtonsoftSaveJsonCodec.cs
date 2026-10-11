using System;

namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5. JSON codec for save payloads; settings in §5.4 step 4.</summary>
    public sealed class NewtonsoftSaveJsonCodec : Game.Core.Save.ISaveJsonCodec
    {
        public byte[] Serialize<T>(T value) { throw new System.NotImplementedException("PERS-004"); }
        public T Deserialize<T>(ReadOnlySpan<byte> utf8) { throw new System.NotImplementedException("PERS-004"); }
    }
}
