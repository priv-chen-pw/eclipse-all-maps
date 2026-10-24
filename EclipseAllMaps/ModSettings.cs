using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// Every setting. Only the host's count, and each is read when it applies, so a change takes effect from
    /// the next stage. The map checkboxes are in <see cref="StageMaps"/>.
    /// </summary>
    internal static class ModSettings
    {
        internal static ConfigEntry<bool> AddAllMaps;
        internal static ConfigEntry<bool> AlsoInNormalRuns;
        internal static ConfigEntry<float> AddedMapChance;
        internal static ConfigEntry<bool> BalanceCredits;

        internal static ConfigEntry<bool> RandomFinalStage;
        internal static ConfigEntry<bool> EndOrLoopAfterFinalBoss;
        internal static ConfigEntry<bool> WeakerSoulboundThreshold;
        internal static ConfigEntry<bool> ShrineOfDescentKeepsTeleporter;

        internal static ConfigEntry<bool> VoidLocusTeleporter;
        internal static ConfigEntry<bool> VoidLocusMonsters;
        internal static ConfigEntry<bool> VoidLocusInteractables;
        internal static ConfigEntry<bool> VoidLocusBosses;

        internal static ConfigEntry<bool> ConduitCanyonToStage5;
        internal static ConfigEntry<bool> RobotBosses;

        internal static ConfigEntry<bool> NoColossusPortals;
        internal static ConfigEntry<bool> HalcyonShrineEveryStage;
        internal static ConfigEntry<bool> WeakerHalcyonShrines;

        internal static ConfigEntry<bool> NoSolutionalHauntPortals;

        internal static ConfigEntry<bool> AlloyHunterBoss;

        internal static ConfigEntry<bool> OnlyAddedMaps;
        internal static ConfigEntry<bool> StartAtStageFive;

        /// <summary>Every setting with its Risk Of Options label, in menu order.</summary>
        private static readonly List<(ConfigEntryBase entry, string label)> menu = new List<(ConfigEntryBase, string)>();

        internal static IEnumerable<(ConfigEntryBase entry, string label)> Menu => menu;

        internal static void Bind(ConfigFile config)
        {
            AddAllMaps = Bind(config, "General", "AddAllMapsToEclipse", "Add all maps to Eclipse", true,
                "The switch for the whole mod: adds Reformed Altar, Treeborn Colony, Conduit Canyon and Void Locus to the normal stage rotation of Eclipse runs, and applies every other setting. Only the host's settings count, and a change applies from the next stage.");
            AlsoInNormalRuns = Bind(config, "General", "AlsoInNormalRuns", "Also in normal runs", false,
                "Applies the mod to normal runs as well as Eclipse. Void Locus is then changed wherever you reach it, through a void portal too.");
            AddedMapChance = config.Bind("General", "AddedMapChance", 1f, new ConfigDescription(
                "How likely each added map is next to a usual map of its stage: 1 is as likely, 2 twice, 0.5 half. To keep one out, switch it off under its stage.",
                new AcceptableValueRange<float>(0.1f, 5f)));
            menu.Add((AddedMapChance, "Added map chance"));
            BalanceCredits = Bind(config, "General", "BalanceCredits", "Balance chest and monster credits", true,
                "Gives each added map the median chest and monster credits of its stage (Void Locus's chest credits go from 220 to 545). Off keeps each map's own.");

            StageMaps.Bind(config);
            menu.AddRange(StageMaps.All.Select(map => ((ConfigEntryBase)map.Enabled, map.Label)));

            // Next to the final stages' checkboxes, the last of StageMaps.
            RandomFinalStage = Bind(config, "Final Stage", "RandomFinalStage", "Random final stage", true,
                "The stage 5 Primordial Teleporter, aligned with the moon, leads to one of the final stages switched on here: Commencement (Mithrix), the Planetarium (Voidling), Prime Meridian (False Son), Solutional Haunt (Solus Wing) or Soulbound Threshold (Anima Harvester). Each needs its DLC. Chat hints at which one, and none comes twice until the run has had them all. Shift Destination still loops. Off, it leads to Commencement as in vanilla.");
            EndOrLoopAfterFinalBoss = Bind(config, "Final Stage", "EndOrLoop", "End or loop after the final boss", true,
                "Each final boss leaves a way to end the run with a win and a way on to the next loop. Commencement and the Planetarium, which already have their ending, get a Destination Portal to stage 1. Solutional Haunt, whose Primordial Teleporter already loops, gets a Celestial Portal that ends the run, and so do Prime Meridian and Soulbound Threshold in Eclipse, which turns their own endings off. That portal only opens there as the final stage. Each stands next to the map's own exit.");
            WeakerSoulboundThreshold = Bind(config, "Final Stage", "WeakerSoulboundThreshold", "Weaker Soulbound Threshold", true,
                "On Soulbound Threshold as the final stage, its monsters have 60% health and the Anima Harvester 80%. Its timer, healing and Hexes stay vanilla.");
            ShrineOfDescentKeepsTeleporter = Bind(config, "Final Stage", "ShrineOfDescentKeepsTeleporter", "Shrine of Descent keeps the teleporter", true,
                "After a Shrine of Descent's 666% charge the teleporter works as usual and no Ritual Portal to Soulbound Threshold opens, since it is a final stage now. Off, the teleporter shuts and the portal opens as in vanilla. Needs Hallowed Concepts.");

            VoidLocusTeleporter = Bind(config, "Void Locus", "PrimordialTeleporter", "Primordial Teleporter", true,
                "Replaces Void Locus's Deep Void Signals with a Primordial Teleporter to the final stage, as on Sky Meadow. Off keeps the signals, so a run that rolls Void Locus goes on to Voidling in the Planetarium.");
            VoidLocusMonsters = Bind(config, "Void Locus", "StageFiveMonsters", "Stage 5 monsters", true,
                "On the first loop, Void Locus spawns half Sky Meadow's monsters and half its own, without Void Jailers. Off, or after a loop, it spawns only its own void monsters.");
            VoidLocusInteractables = Bind(config, "Void Locus", "StageFiveInteractables", "Stage 5 interactables", true,
                "Gives Void Locus Sky Meadow's chests, shrines, printers and drones, with its own void interactables about as common as chests. Off keeps only Void Cradles, Void Potentials and Stalks.");
            VoidLocusBosses = Bind(config, "Void Locus", "VoidBosses", "Void bosses", true,
                "Void Locus's teleporter boss is a Void Devastator, Imp Overlord, Xi Construct, Wandering Vagrant or Grandparent, each as likely among those the boss credits can buy. Off, it comes from the stage's monsters like on any map (and can be the Alloy Hunter): on the first loop half the time a Void Devastator, after that always.");

            ConduitCanyonToStage5 = Bind(config, "Conduit Canyon", "TeleporterToStageFive", "Teleporter to stage 5", true,
                "Conduit Canyon's teleporter leads to stage 5. Off, it leads down the Alloyed path to Solutional Haunt, as in vanilla.");
            RobotBosses = Bind(config, "Conduit Canyon", "RobotBosses", "Robot bosses", true,
                "Conduit Canyon's teleporter boss is a Solus Control Unit, Alloy Worship Unit, Solus Amalgamator or Alloy Hunter, each as likely, in one fight. The Alloy Worship Unit and the Alloy Hunter come one at a time, as with Alloy Hunter as a normal boss. Off, the boss is its usual Solus Amalgamator, or the Alloy Hunter with that setting on; with both off, vanilla's scripted two-boss fight stays.");

            NoColossusPortals = Bind(config, "Colossus Path", "NoGreenPortals", "No green portals", true,
                "No green portal down the Colossus path opens: not from Reformed Altar's and Treeborn Colony's teleporters, Halcyon Shrines, Gilded Coast or the Bazaar's geodes, so the run stays on the stage rotation. Off, they open as in vanilla.");
            HalcyonShrineEveryStage = Bind(config, "Colossus Path", "HalcyonShrineEveryStage", "Halcyon Shrine on every stage", true,
                "Halcyon Shrines can spawn on every stage, as one of its interactables with their usual cost and odds. Vanilla only has them when a stage's Seekers of the Storm content is rolled in, and never on Conduit Canyon or Void Locus. Needs Seekers of the Storm.");
            WeakerHalcyonShrines = Bind(config, "Colossus Path", "WeakerHalcyonShrinesFromStageFour", "Weaker Halcyon Shrines from stage 4", true,
                "From stage 4 on, a Halcyon Shrine's monster waves get half the credits, and its Halcyonite half the bonus from the gold drained: at most x5.2 health and x2.75 damage instead of x10 and x4.75. Its cost and reward stay vanilla.");

            NoSolutionalHauntPortals = Bind(config, "Alloyed Path", "NoSolutionalHauntPortals", "No Solutional Haunt portals", true,
                "Solved access codes and Siren's Call open no portal to Solutional Haunt, so the run stays on the stage rotation; the access codes' extra boss and item stay. A stage 3's access codes still open their portal to Conduit Canyon, a stage 4 of the rotation, unless Conduit Canyon is switched off. Off, they open as in vanilla.");

            // Key kept from when it only covered stage 4+, so existing configs keep their choice.
            AlloyHunterBoss = Bind(config, "Bosses", "AlloyHunterFromStageFour", "Alloy Hunter as a normal boss", true,
                "From stage 4 on the Alloy Hunter can be the teleporter boss on any map (but Void Locus with Void bosses on), as likely as any one of that map's own bosses, and Conduit Canyon's teleporter event has one boss. It comes one at a time, as vanilla only ever spawns it: one, plus one per Shrine of the Mountain, each with the health of the hunters the boss credits would buy, so the fight has an 800-credit boss's total health in fewer hunters. Off, it only shows up as vanilla's second boss on Conduit Canyon, killing and replacing a Solus Control Unit. Needs the Alloyed Collective DLC.");

            OnlyAddedMaps = Bind(config, "Debug", "OnlyAddedMaps", "Only added maps", false,
                "Gives every other map a 0% chance on the stages that gained a map, and keeps them off the Bazaar's Lunar Seers, so an Eclipse run with every map switched on always goes stage 1 -> Reformed Altar -> Treeborn Colony -> Conduit Canyon -> Void Locus -> a final stage. For testing the added maps; leave off for normal play.");
            StartAtStageFive = Bind(config, "Debug", "StartAtStageFive", "Start at stage 5", false,
                "The run starts on a stage 5 map, and loops back to one. Every player starts with 20 Mochas, 20 Hopoo Feathers, 10 Focused Convergences and god mode. For testing the final stages; with Only added maps on, stage 5 is always Void Locus. Leave off for normal play.");
        }

        /// <summary>
        /// Whether the mod applies to <paramref name="run"/>'s game mode: Eclipse, or a normal run with
        /// <see cref="AlsoInNormalRuns"/>. Simulacrum and the Prismatic Trials never.
        /// </summary>
        internal static bool AppliesTo(Run run) =>
            run is EclipseRun || (AlsoInNormalRuns.Value && run && run.GetType() == typeof(Run));

        private static ConfigEntry<bool> Bind(ConfigFile config, string section, string key, string label, bool defaultValue, string description)
        {
            ConfigEntry<bool> entry = config.Bind(section, key, defaultValue, description);
            menu.Add((entry, label));
            return entry;
        }
    }
}
