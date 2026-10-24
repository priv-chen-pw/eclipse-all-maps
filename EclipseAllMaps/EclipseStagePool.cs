using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// Adds <see cref="AddedMaps"/> to the stage pool every time an Eclipse run picks its next stage, and
    /// leaves out every map switched off in <see cref="StageMaps"/>.
    ///
    /// Every pick goes through <c>Run.PickNextStageScene</c>: stage 1 from <c>Run.startingSceneGroup</c> in
    /// <c>Run.Start</c>, and every later stage from the current map's <c>destinationsGroup</c> (sgStageN, or
    /// loopSgStageN after looping) as soon as that map's teleporter spawns. Editing the selection there,
    /// rather than those shared SceneCollection assets, leaves every other game mode vanilla and lets the
    /// checkboxes take effect on the very next pick with nothing to undo.
    ///
    /// Nothing else is needed for the run to carry on afterwards: each added map's own teleporter already
    /// leads into the next stage's normal pool.
    /// </summary>
    internal static class EclipseStagePool
    {
        /// <summary>
        /// An added map's weight in its stage pool: <see cref="ModSettings.AddedMapChance"/> times the weight
        /// every vanilla stage 2-5 entry has, 1.
        /// </summary>
        private static float AddedMapWeight => ModSettings.AddedMapChance.Value;

        internal static void Init()
        {
            On.RoR2.Run.PickNextStageScene += (orig, self, choices) =>
            {
                if (IsActive(self))
                {
                    ApplyMapSettings(self, choices);
                }

                orig(self, choices);
            };

            // The Bazaar's Lunar Seers offer every map that shares the next stage's stageOrder (plus, by chance,
            // the Bazaar's own seerSceneOverrides), and buying one sends the run there directly, past
            // PickNextStageScene. So for this one call:
            // - Maps placed on another stage file under it. Void Locus's own stageOrder is 99, which it shares
            //   with the Planetarium and A Moment, Whole, so with Void Locus up next a seer could skip the rest
            //   of the run. Not gated on the checkboxes: a map switched off after it was already picked still
            //   needs it.
            // - Maps the stage pool leaves out (switched off, or every vanilla one under OnlyAddedMaps) are
            //   filtered out of the Bazaar and dropped from the overrides, so a seer offers a map the pool
            //   could roll or, as vanilla does when none qualifies, nothing.
            On.RoR2.BazaarController.SetUpSeerStations += (orig, self) =>
            {
                Run run = Run.instance;
                if (!ModSettings.AppliesTo(run))
                {
                    orig(self);
                    return;
                }

                var undo = new List<Action>();
                try
                {
                    RefileStageOrders(run, undo);

                    if (IsActive(run))
                    {
                        FilterLeftOutMapsOutOfBazaar(run, self, undo);
                    }

                    orig(self);
                }
                finally
                {
                    for (int i = undo.Count - 1; i >= 0; i--)
                    {
                        undo[i]();
                    }
                }
            };

            SceneCatalog.availability.CallWhenAvailable(LogAddedMaps);
        }

        /// <summary>
        /// Whether the mod is on for <paramref name="run"/>: an Eclipse run (or a normal one, if set) with
        /// <see cref="ModSettings.AddAllMaps"/> on.
        /// </summary>
        internal static bool IsActive(Run run) => ModSettings.AppliesTo(run) && ModSettings.AddAllMaps.Value;

        private static void ApplyMapSettings(Run run, WeightedSelection<SceneDef> choices)
        {
            // Random stage order ignores the choices and rolls from every stage in the game.
            if (run.ruleBook.stageOrder != StageOrder.Normal || choices.Count == 0)
            {
                return;
            }

            // The run_scene_override cheat forces the first stage; keep it forced.
            if (!run.nextStageScene && !string.IsNullOrEmpty(RoR2.Console.instance?.FindConVar("run_scene_override")?.GetString()))
            {
                return;
            }

            DebugStageFive.ReplaceStageOne(run, choices);

            // A pool only ever holds one stage's maps, so any entry tells which stage is being picked.
            int stage = choices.GetChoice(0).value.stageOrder;
            List<SceneDef> addedMaps = MapsForStage(run, stage);
            Predicate<SceneDef> canRoll = PoolFilter(addedMaps);

            var kept = new List<(SceneDef map, float weight)>();
            for (int i = 0; i < choices.Count; i++)
            {
                WeightedSelection<SceneDef>.ChoiceInfo choice = choices.GetChoice(i);
                if (canRoll(choice.value))
                {
                    kept.Add((choice.value, choice.weight));
                }
            }

            foreach (SceneDef map in addedMaps)
            {
                if (!kept.Exists(entry => entry.map == map))
                {
                    kept.Add((map, AddedMapWeight));
                }
            }

            // Never leave a stage with nothing to roll (weight 0 entries can't be rolled either).
            if (kept.Sum(entry => entry.weight) <= 0f)
            {
                Log.Warning($"Every map that could roll on stage {stage} is switched off, so it keeps its usual maps.");
                return;
            }

            choices.Clear();
            foreach ((SceneDef map, float weight) in kept)
            {
                choices.AddChoice(map, weight);
            }

            Log.Info($"Stage {stage} pool{(ModSettings.OnlyAddedMaps.Value && addedMaps.Count > 0 ? " (only added maps)" : "")}: {Describe(choices)}");
        }

        /// <summary>
        /// Which of a stage's maps its pool may hold: those switched on, or under OnlyAddedMaps only its
        /// <paramref name="addedMaps"/> (when it has any).
        /// </summary>
        private static Predicate<SceneDef> PoolFilter(List<SceneDef> addedMaps) =>
            // Start at stage 5 is for testing the stage 5 pool as it is, Void Locus among the others.
            ModSettings.OnlyAddedMaps.Value && !ModSettings.StartAtStageFive.Value && addedMaps.Count > 0
                ? (Predicate<SceneDef>)addedMaps.Contains
                : StageMaps.IsEnabled;

        /// <summary>The added maps for <paramref name="stage"/> that are switched on and this run can load.</summary>
        private static List<SceneDef> MapsForStage(Run run, int stage)
        {
            var maps = new List<SceneDef>();

            foreach (AddedMap addedMap in AddedMaps.All)
            {
                if (!addedMap.Enabled)
                {
                    continue;
                }

                SceneDef sceneDef = SceneCatalog.FindSceneDef(addedMap.SceneName);
                if (!sceneDef || addedMap.StageOf(sceneDef) != stage)
                {
                    continue;
                }

                sceneDef = LoopedVariant(run, sceneDef);

                // False when the map's DLC is off for this run.
                if (run.CanPickStage(sceneDef))
                {
                    maps.Add(sceneDef);
                }
            }

            return maps;
        }

        /// <summary>
        /// Vanilla's own test for swapping a path stage for its looped version, copied from
        /// <c>SceneExitController.GetLoopedStageOrDefault</c>.
        /// </summary>
        private static SceneDef LoopedVariant(Run run, SceneDef sceneDef)
        {
            SceneDef looped = sceneDef.loopedSceneDef;
            return run.loopClearCount > 0 && run.stageClearCount > 5 && looped ? looped : sceneDef;
        }

        /// <summary>
        /// Gives every added map in play the stageOrder of the stage it joins, adding the restore to
        /// <paramref name="undo"/>. In play means switched on, or already picked as the next stage.
        /// </summary>
        private static void RefileStageOrders(Run run, List<Action> undo)
        {
            foreach (AddedMap addedMap in AddedMaps.All)
            {
                SceneDef sceneDef = SceneCatalog.FindSceneDef(addedMap.SceneName);
                if (!sceneDef)
                {
                    continue;
                }

                // A map switched off stays out of the seers too, unless it is already the next stage.
                bool switchedOn = ModSettings.AddAllMaps.Value && addedMap.Enabled;
                if (!switchedOn && sceneDef != run.nextStageScene)
                {
                    continue;
                }

                int stage = addedMap.StageOf(sceneDef);
                int stageOrder = sceneDef.stageOrder;
                if (stage != stageOrder)
                {
                    sceneDef.stageOrder = stage;
                    undo.Add(() => sceneDef.stageOrder = stageOrder);
                }
            }
        }

        /// <summary>
        /// Filters the next stage's maps that its pool leaves out (<see cref="PoolFilter"/>) out of the
        /// Bazaar and drops seer overrides to them, adding the restores to <paramref name="undo"/>.
        /// </summary>
        private static void FilterLeftOutMapsOutOfBazaar(Run run, BazaarController bazaar, List<Action> undo)
        {
            if (!run.nextStageScene)
            {
                return;
            }

            // Read after RefileStageOrders, so an added map up next already counts under its new stage.
            int stage = run.nextStageScene.stageOrder;
            Predicate<SceneDef> canRoll = PoolFilter(MapsForStage(run, stage));

            var leftOut = new List<string>();
            foreach (SceneDef sceneDef in SceneCatalog.allSceneDefs)
            {
                if (sceneDef.stageOrder == stage && !sceneDef.filterOutOfBazaar && !canRoll(sceneDef))
                {
                    sceneDef.filterOutOfBazaar = true;
                    undo.Add(() => sceneDef.filterOutOfBazaar = false);
                    leftOut.Add(sceneDef.cachedName);
                }
            }

            BazaarController.SeerSceneOverride[] overrides = bazaar.seerSceneOverrides;
            bazaar.seerSceneOverrides = Array.FindAll(overrides, seerOverride => canRoll(seerOverride.sceneDef));
            undo.Add(() => bazaar.seerSceneOverrides = overrides);

            if (leftOut.Count > 0)
            {
                Log.Info($"Bazaar seers for stage {stage} leave out: {string.Join(", ", leftOut)}");
            }
        }

        private static string Describe(WeightedSelection<SceneDef> choices)
        {
            float totalWeight = choices.GetTotalWeight();
            var text = new StringBuilder();

            for (int i = 0; i < choices.Count; i++)
            {
                WeightedSelection<SceneDef>.ChoiceInfo choice = choices.GetChoice(i);
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(choice.value.cachedName).Append(' ').Append((choice.weight / totalWeight).ToString("P0"));
            }

            return text.ToString();
        }

        private static void LogAddedMaps()
        {
            foreach (AddedMap addedMap in AddedMaps.All)
            {
                SceneDef sceneDef = SceneCatalog.FindSceneDef(addedMap.SceneName);
                if (!sceneDef)
                {
                    Log.Warning($"This game build has no '{addedMap.SceneName}' scene; it will not be added to Eclipse.");
                    continue;
                }

                Log.Info($"{Language.GetString(sceneDef.nameToken)} ({sceneDef.cachedName}) joins Eclipse stage {addedMap.StageOf(sceneDef)}.");
            }
        }
    }
}
