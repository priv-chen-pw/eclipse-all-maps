using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// Every setting. Only the host's count, and each is read when it applies, so a change takes effect from
    /// the next stage. The map checkboxes are in <see cref="StageMaps"/>.
    ///
    /// Kept to what can be switched either way without breaking the run: the mod itself, which added maps and
    /// final stages roll, and the two extras (Halcyon Shrines everywhere, the Alloy Hunter as a boss). The rest
    /// of the mod always applies: an off switch there reopened a route off the rotation, took a final stage's
    /// only win away, or undid the mod's own balancing.
    /// </summary>
    internal static class ModSettings
    {
        internal static ConfigEntry<bool> AddAllMaps;
        internal static ConfigEntry<float> AddedMapChance;
        internal static ConfigEntry<bool> HalcyonShrineEveryStage;
        internal static ConfigEntry<bool> AlloyHunterBoss;

        internal static ConfigEntry<bool> OnlyAddedMaps;
        internal static ConfigEntry<bool> StartAtStageFive;
        internal static ConfigEntry<bool> StartAtPrimeMeridian;

        /// <summary>Every setting with its Risk Of Options label, in menu order.</summary>
        private static readonly List<(ConfigEntryBase entry, string label)> menu = new List<(ConfigEntryBase, string)>();

        internal static IEnumerable<(ConfigEntryBase entry, string label)> Menu => menu;

        internal static void Bind(ConfigFile config)
        {
            AddAllMaps = Bind(config, "General", "AddAllMapsToEclipse", "Add all maps to Eclipse", true,
                "The switch for the whole mod: adds Reformed Altar, Treeborn Colony, Conduit Canyon and Void Locus to the normal stage rotation of Eclipse runs, sends stage 5 to a random final stage, and applies every other change. Only the host's settings count, and a change applies from the next stage.");
            AddedMapChance = config.Bind("General", "AddedMapChance", 1f, new ConfigDescription(
                "How likely each added map is next to a usual map of its stage: 1 is as likely, 2 twice, 0.5 half. To keep one out, switch it off under Maps.",
                new AcceptableValueRange<float>(0.1f, 5f)));
            menu.Add((AddedMapChance, "Added map chance"));
            HalcyonShrineEveryStage = Bind(config, "General", "HalcyonShrineEveryStage", "Halcyon Shrine on every stage", true,
                "Halcyon Shrines can spawn on every stage, as one of its interactables with their usual cost and odds. Vanilla only has them when a stage's Seekers of the Storm content is rolled in, and never on Conduit Canyon or Void Locus. Needs Seekers of the Storm.");
            AlloyHunterBoss = Bind(config, "General", "AlloyHunterBoss", "Alloy Hunter as a normal boss", true,
                "From stage 4 on the Alloy Hunter can be the teleporter boss on any map, as likely as any one of that map's own bosses. It comes one at a time: one, plus one per Shrine of the Mountain, with the total health of a normal boss fight. Off, it only comes as one of Conduit Canyon's robot bosses. Needs the Alloyed Collective.");

            StageMaps.Bind(config);
            menu.AddRange(StageMaps.All.Select(map => ((ConfigEntryBase)map.Enabled, map.Label)));

            OnlyAddedMaps = Bind(config, "Debug", "OnlyAddedMaps", "Only added maps", false,
                "Gives every other map a 0% chance on the stages that gained a map, and keeps them off the Bazaar's Lunar Seers, so an Eclipse run with every map switched on always goes stage 1 -> Reformed Altar -> Treeborn Colony -> Conduit Canyon -> Void Locus -> a final stage. For testing the added maps; leave off for normal play.");
            StartAtStageFive = Bind(config, "Debug", "StartAtStageFive", "Start at stage 5", false,
                "The run starts on a stage 5 map, and loops back to one. Every player starts with 20 Mochas, 20 Hopoo Feathers, 10 Focused Convergences and god mode. For testing the final stages; with Only added maps on, stage 5 is always Void Locus. Leave off for normal play.");
            StartAtPrimeMeridian = Bind(config, "Debug", "StartAtPrimeMeridian", "Start at Prime Meridian", false,
                "The run starts on Prime Meridian as the final stage (stage 6), as if it came from stage 5, so False Son leaves the altar that ends the run. Every player starts with 20 Mochas, 20 Hopoo Feathers, 10 Focused Convergences and god mode. For testing the end altar; needs Seekers of the Storm. Leave off for normal play.");

            // Settings that are gone would stay in the file, doing nothing. Private in BepInEx 5.4.21, hence the guard.
            if (typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(config)
                is Dictionary<ConfigDefinition, string> orphans && orphans.Count > 0)
            {
                Log.Info($"Dropping {orphans.Count} settings this version no longer has from the config file.");
                orphans.Clear();
                config.Save();
            }
        }

        /// <summary>Whether the mod applies to <paramref name="run"/>'s game mode: Eclipse only.</summary>
        internal static bool AppliesTo(Run run) => run is EclipseRun;

        private static ConfigEntry<bool> Bind(ConfigFile config, string section, string key, string label, bool defaultValue, string description)
        {
            ConfigEntry<bool> entry = config.Bind(section, key, defaultValue, description);
            menu.Add((entry, label));
            return entry;
        }
    }
}
