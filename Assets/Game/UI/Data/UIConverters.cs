using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Data
{
    /// <summary>SPEC-60 §5.6: named, pure converters shared by runtime UXML bindings.</summary>
    public static class UIConverters
    {
        public static void RegisterAll()
        {
            if (!ConverterGroups.TryGetConverterGroup("fraction-to-percent", out _))
            {
                var group = new ConverterGroup("fraction-to-percent");
                group.AddConverter<float, StyleLength>(FractionToPercent);
                ConverterGroups.RegisterConverterGroup(group);
            }
            if (!ConverterGroups.TryGetConverterGroup("int-to-text", out _))
            {
                var group = new ConverterGroup("int-to-text");
                group.AddConverter<int, string>(IntToText);
                ConverterGroups.RegisterConverterGroup(group);
            }
            if (!ConverterGroups.TryGetConverterGroup("bool-to-display", out _))
            {
                var group = new ConverterGroup("bool-to-display");
                group.AddConverter<bool, StyleEnum<DisplayStyle>>(BoolToDisplay);
                ConverterGroups.RegisterConverterGroup(group);
            }
            if (!ConverterGroups.TryGetConverterGroup("seconds-to-clock", out _))
            {
                var group = new ConverterGroup("seconds-to-clock");
                group.AddConverter<float, string>(SecondsToClock);
                ConverterGroups.RegisterConverterGroup(group);
            }
        }

        static StyleLength FractionToPercent(ref float value) => new StyleLength(Length.Percent(Mathf.Clamp01(value) * 100f));
        static string IntToText(ref int value) => NumberStringCache.Get(value);
        static StyleEnum<DisplayStyle> BoolToDisplay(ref bool value) => value ? DisplayStyle.Flex : DisplayStyle.None;
        static string SecondsToClock(ref float value)
        {
            // Invalid/negative durations display zero; fractions truncate, and long durations keep their minutes.
            int seconds = float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? 0 : (int)Math.Min(Math.Floor((double)value), int.MaxValue);
            int minutes = seconds / 60;
            return string.Concat(minutes < 100 ? NumberStringCache.Get2Digits(minutes) : NumberStringCache.Get(minutes),
                ":", NumberStringCache.Get2Digits(seconds % 60));
        }
    }
}
