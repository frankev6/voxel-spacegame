using System;
using System.Globalization;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    internal static class GalaxyNames
    {
        internal static string System(GalaxyDef galaxy, StarSystemId id)
        {
            var system = galaxy.Get(id);
            return system.AuthoredIndex > 0 ? galaxy.Authored[system.AuthoredIndex - 1].DisplayName
                : galaxy.Templates[system.TemplateIndex].NamePrefix + " " + (char)('A' + system.Seed % 26) + "-" + id.Value.ToString("D4", CultureInfo.InvariantCulture);
        }
        internal static string Roman(int value)
        {
            if (value < 1 || value > 20) throw new ArgumentOutOfRangeException(nameof(value));
            string tens = value >= 20 ? "XX" : value >= 10 ? "X" : "";
            return tens + ((value % 10) switch { 0 => "", 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", _ => "IX" });
        }
    }
}
