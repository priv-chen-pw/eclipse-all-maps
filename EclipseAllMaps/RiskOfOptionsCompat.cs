using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace EclipseAllMaps
{
    /// <summary>
    /// Shows the settings under the game's Settings > Mod Options when Risk Of Options is installed. It is
    /// optional: without it the same settings are in the config file, which r2modman's config editor shows.
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        internal const string PluginGuid = "com.rune580.riskofoptions";

        internal static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(PluginGuid);

        /// <summary>
        /// Call only when <see cref="IsInstalled"/>. Kept out of line so Risk Of Options' types are resolved
        /// when this runs, never when the mod loads without it. Each option is grouped under its config
        /// section and named by its label: a checkbox for a bool, a slider over its acceptable range for a
        /// float.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void AddOptions(IEnumerable<(ConfigEntryBase entry, string label)> options)
        {
            ModSettingsManager.SetModDescription(
                "Adds every map to the normal Eclipse stage rotation.",
                EclipseAllMapsPlugin.PluginGuid,
                EclipseAllMapsPlugin.PluginName);

            foreach ((ConfigEntryBase entry, string label) in options)
            {
                BaseOption option;
                switch (entry)
                {
                    case ConfigEntry<bool> checkBox:
                        option = new CheckBoxOption(checkBox, new CheckBoxConfig { name = label });
                        break;
                    case ConfigEntry<float> slider when slider.Description.AcceptableValues is AcceptableValueRange<float> range:
                        option = new StepSliderOption(slider, new StepSliderConfig
                        {
                            name = label,
                            min = range.MinValue,
                            max = range.MaxValue,
                            increment = 0.1f,
                            FormatString = "{0:0.0}x",
                        });
                        break;
                    default:
                        continue;
                }

                ModSettingsManager.AddOption(option, EclipseAllMapsPlugin.PluginGuid, EclipseAllMapsPlugin.PluginName);
            }
        }
    }
}
