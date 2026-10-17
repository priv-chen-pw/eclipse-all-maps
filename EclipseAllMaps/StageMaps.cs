using System.Collections.Generic;
using BepInEx.Configuration;
using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// A checkbox for every map an Eclipse run can roll on stages 1-5, in a config section per stage: the
    /// vanilla pools (sgStageN and loopSgStageN, as shipped) plus <see cref="AddedMaps"/>. A map switched off
    /// is left out of the stage pool and the Bazaar's seers. A map not listed here (a later patch's) stays on.
    /// </summary>
    internal static class StageMaps
    {
        private const string FirstLoopOnly = " Only rolls on the first loop.";
        private const string AfterLoopingOnly = " Only rolls after looping.";
        private const string SecondLayout = " Its second layout.";

        internal static readonly StageMap[] All =
        {
            new StageMap(1, "blackbeach", "Distant Roost"),
            new StageMap(1, "blackbeach2", "Distant Roost", SecondLayout),
            new StageMap(1, "golemplains", "Titanic Plains"),
            new StageMap(1, "golemplains2", "Titanic Plains", SecondLayout),
            new StageMap(1, "snowyforest", "Siphoned Forest"),
            new StageMap(1, "lakes", "Verdant Falls", FirstLoopOnly),
            new StageMap(1, "lakesnight", "Viscous Falls", AfterLoopingOnly),
            new StageMap(1, "village", "Shattered Abodes", FirstLoopOnly),
            new StageMap(1, "villagenight", "Disturbed Impact", AfterLoopingOnly),

            new StageMap(2, "goolake", "Abandoned Aqueduct"),
            new StageMap(2, "foggyswamp", "Wetland Aspect"),
            new StageMap(2, "ancientloft", "Aphelian Sanctuary"),
            new StageMap(2, "nest", "Pretender's Precipice"),
            new StageMap(2, "lemuriantemple", "Reformed Altar", " Added by this mod, without its green Colossus portal."),

            new StageMap(3, "frozenwall", "Rallypoint Delta"),
            new StageMap(3, "wispgraveyard", "Scorched Acres"),
            new StageMap(3, "sulfurpools", "Sulfur Pools"),
            new StageMap(3, "ironalluvium", "Iron Alluvium", FirstLoopOnly),
            new StageMap(3, "ironalluvium2", "Iron Auroras", AfterLoopingOnly),
            new StageMap(3, "mire", "Tenebrous Quagmire"),
            new StageMap(3, "fungal", "Damp Caverns"),
            new StageMap(3, "habitat", "Treeborn Colony", " Added by this mod, without its green Colossus portal; Golden Dieback after looping."),

            new StageMap(4, "dampcavesimple", "Abyssal Depths"),
            new StageMap(4, "shipgraveyard", "Siren's Call"),
            new StageMap(4, "rootjungle", "Sundered Grove"),
            new StageMap(4, "repurposedcrater", "Repurposed Crater"),
            new StageMap(4, "sanctum", "Lurid Basilica"),
            new StageMap(4, "conduitcanyon", "Conduit Canyon",
                " Added by this mod. Its teleporter leads to stage 5 instead of the Alloyed path, with one boss."),

            new StageMap(5, "skymeadow", "Sky Meadow"),
            new StageMap(5, "helminthroost", "Helminth Hatchery"),
            new StageMap(5, "voidstage", "Void Locus",
                " Added by this mod. Instead of Deep Void Signals it has a Primordial Teleporter, leading to Commencement (Mithrix) like Sky Meadow."),
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
        internal readonly int Stage;
        internal readonly string SceneName;

        /// <summary>The in-game name with the scene name, which tells the layouts of a map apart.</summary>
        internal readonly string Label;

        private readonly string note;

        internal StageMap(int stage, string sceneName, string name, string note = "")
        {
            Stage = stage;
            SceneName = sceneName;
            Label = $"{name} ({sceneName})";
            this.note = note;
        }

        internal ConfigEntry<bool> Enabled { get; private set; }

        // Keyed by scene name: BepInEx keys can't hold the apostrophes in some map names.
        internal void Bind(ConfigFile config) => Enabled = config.Bind(
            $"Stage {Stage}",
            SceneName,
            true,
            $"{Label}: switch off to keep it out of Eclipse runs.{note}");
    }
}
