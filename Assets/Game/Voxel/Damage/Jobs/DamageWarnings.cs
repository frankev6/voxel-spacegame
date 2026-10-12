using Unity.Collections;

namespace Game.Voxel.Damage.Jobs
{
    internal static class DamageWarnings
    {
        public static void RadiusClamped()
        {
            var message = default(FixedString128Bytes);
            // Unmanaged construction is required by Burst (SPEC-13 Q22).
            message.Append('['); message.Append('D'); message.Append('a'); message.Append('m'); message.Append('a'); message.Append('g');
            message.Append('e'); message.Append(']'); message.Append(' '); message.Append('b'); message.Append('l'); message.Append('a');
            message.Append('s'); message.Append('t'); message.Append(' '); message.Append('r'); message.Append('a'); message.Append('d');
            message.Append('i'); message.Append('u'); message.Append('s'); message.Append(' '); message.Append('c'); message.Append('l');
            message.Append('a'); message.Append('m'); message.Append('p'); message.Append('e'); message.Append('d'); message.Append(' ');
            message.Append('t'); message.Append('o'); message.Append(' '); message.Append('c'); message.Append('o'); message.Append('n');
            message.Append('f'); message.Append('i'); message.Append('g'); message.Append('u'); message.Append('r'); message.Append('e');
            message.Append('d'); message.Append(' '); message.Append('c'); message.Append('e'); message.Append('l'); message.Append('l');
            message.Append(' '); message.Append('c'); message.Append('a'); message.Append('p'); message.Append('.');
            Game.Core.GameLog.Burst.Warn(message);
        }
        public static void BadEvent()
        {
            var message = default(FixedString128Bytes);
            message.Append('['); message.Append('D'); message.Append('a'); message.Append('m'); message.Append('a'); message.Append('g');
            message.Append('e'); message.Append(']'); message.Append(' '); message.Append('b'); message.Append('a'); message.Append('d');
            message.Append('-'); message.Append('e'); message.Append('v'); message.Append('e'); message.Append('n'); message.Append('t');
            message.Append(':'); message.Append(' '); message.Append('n'); message.Append('o'); message.Append('n'); message.Append('-');
            message.Append('f'); message.Append('i'); message.Append('n'); message.Append('i'); message.Append('t'); message.Append('e');
            message.Append(' '); message.Append('f'); message.Append('i'); message.Append('e'); message.Append('l'); message.Append('d');
            message.Append(';'); message.Append(' '); message.Append('d'); message.Append('r'); message.Append('o'); message.Append('p');
            message.Append('p'); message.Append('e'); message.Append('d'); message.Append('.');
            Game.Core.GameLog.Burst.Warn(message);
        }
    }
}
