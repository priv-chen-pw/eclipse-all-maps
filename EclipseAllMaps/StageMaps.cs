using System.Collections.Generic;
using BepInEx.Configuration;
using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// A checkbox for each map the mod adds to the rotation, and for each final stage <see cref="FinalStage"/>
    /// picks from after stage 5. An added map switched off is left out of its stage pool and the Bazaar's seers;
    /// any other map has no checkbox and stays on.
    /// </summary>
    internal static class StageMaps
    {
        private const string MapsSection = "Maps";
        private const string FinalStageSection = "Final Stage";
        private const string FinalStageNote = " One of the final stages the stage 5 teleporter picks from; with every one off it leads to Commencement.";

        internal static readonly StageMap[] All =
        {
            new StageMap(MapsSection, "lemuriantemple", "Reformed Altar", " Stage 2, without its green Colossus portal."),
            new StageMap(MapsSection, "habitat", "Treeborn Colony", " Stage 3, without its green Colossus portal; Golden Dieback after looping."),
            new StageMap(MapsSection, "conduitcanyon", "Conduit Canyon",
                " Stage 4. Its teleporter leads to stage 5 instead of the Alloyed path, and its boss is one random robot boss."),
            new StageMap(MapsSection, "voidstage", "Void Locus",
                " Stage 5. Instead of Deep Void Signals it has a Primordial Teleporter, leading to the final stage like Sky Meadow's, and stage 5 chests, monsters and bosses mixed in."),

            new StageMap(FinalStageSection, "moon2", "Commencement", " Mithrix." + FinalStageNote),
            new StageMap(FinalStageSection, "voidraid", "The Planetarium", " Voidling. Needs Survivors of the Void." + FinalStageNote),
            new StageMap(FinalStageSection, "meridian", "Prime Meridian", " False Son. Needs Seekers of the Storm." + FinalStageNote),
            new StageMap(FinalStageSection, "solutionalhaunt", "Solutional Haunt", " Solus Wing. Needs the Alloyed Collective." + FinalStageNote),
            new StageMap(FinalStageSection, "champrush", "Soulbound Threshold", " The Anima Harvester. Needs Hallowed Concepts." + FinalStageNote),
        };

        private static readonly Dictionary<string, StageMap> bySceneName = new Dictionary<string, StageMap>();

        internal static void Bind(ConfigFile config)
        {
            foreach (StageMap map in All)
            {
                map.Bind(config);
                bySceneName[map.SceneName] = map;
            }
        }

        internal static bool IsEnabled(SceneDef sceneDef) => sceneDef && IsEnabled(sceneDef.cachedName);

        /// <summary>Whether the map's checkbox is on; true for a map without one.</summary>
        internal static bool IsEnabled(string sceneName) =>
            !bySceneName.TryGetValue(sceneName, out StageMap map) || map.Enabled.Value;
    }

    internal sealed class StageMap
    {
        internal readonly string SceneName;

        /// <summary>The in-game name.</summary>
        internal readonly string Label;

        private readonly string section;
        private readonly string note;

        internal StageMap(string section, string sceneName, string name, string note = "")
        {
            this.section = section;
            SceneName = sceneName;
            Label = name;
            this.note = note;
        }

        internal ConfigEntry<bool> Enabled { get; private set; }

        // Keyed by scene name: BepInEx keys can't hold the apostrophes in some map names.
        internal void Bind(ConfigFile config) => Enabled = config.Bind(
            section,
            SceneName,
            true,
            $"{Label}: switch off to keep it out of Eclipse runs.{note}");
    }
}
