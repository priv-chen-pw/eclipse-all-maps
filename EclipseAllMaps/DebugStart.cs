using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// Debug starts, for testing the final stages. With either, every player starts with 20 Mochas, 20 Hopoo
    /// Feathers, 10 Focused Convergences and god mode.
    ///
    /// - Start at stage 5: every stage 1 pick (the run's first stage, and each loop's after a final stage) draws
    ///   from the whole stage 5 pool instead (Only added maps aside), and the run counts stages 1-4 as cleared.
    ///   Leaving the stage 5 begins a loop as usual, so the stage after it counts as the final stage.
    /// - Start at Prime Meridian: the run's first stage is Prime Meridian as the final stage, stage 6, as if it
    ///   came from a stage 5: five stages cleared and a loop begun on the fifth. False Son then leaves the end altar.
    /// </summary>
    internal static class DebugStart
    {
        /// <summary>
        /// Stages cleared before stage 5. Without them the run would be at stage 1 difficulty, where the boss credits
        /// (600 × √difficulty) buy no stage 5 boss: those cost 800 and more.
        /// </summary>
        private const int StagesBeforeFive = 4;

        private const string PrimeMeridian = "meridian";

        private const int Mochas = 20;
        private const int Feathers = 20;

        /// <summary>The lunar item that speeds up teleporter charging.</summary>
        private const int FocusedConvergences = 10;

        internal static void Init()
        {
            // After Run.Start has begun the first loop and asked for the first scene, so the loop counts from 0 and
            // the stage 5 is the loop's fifth stage, as in a real run.
            Run.onRunStartGlobal += run =>
            {
                if (!UnityEngine.Networking.NetworkServer.active || !Applies(run) || run.stageClearCount != 0)
                {
                    return;
                }

                if (run.nextStageScene && run.nextStageScene.cachedName == PrimeMeridian)
                {
                    // What leaving a stage 5 leaves behind: its clear counted, then a loop begun from there.
                    run.NetworkstageClearCount = StagesBeforeFive + 1;
                    run.NetworkstageClearCountAtLoopStart = StagesBeforeFive + 1;
                    run.Network_loopClearCount = 1;
                    Log.Info($"Debug: {StagesBeforeFive + 1} stages counted as cleared and a loop begun.");
                }
                else if (ModSettings.StartAtStageFive.Value)
                {
                    run.NetworkstageClearCount = StagesBeforeFive;
                    Log.Info($"Debug: {StagesBeforeFive} stages counted as cleared.");
                }
            };

            Run.onPlayerFirstCreatedServer += (run, player) =>
            {
                if (Applies(run) && player.master)
                {
                    Inventory inventory = player.master.inventory;
                    inventory.GiveItemPermanent(DLC1Content.Items.AttackSpeedAndMoveSpeed, Mochas);
                    inventory.GiveItemPermanent(RoR2Content.Items.Feather, Feathers);
                    inventory.GiveItemPermanent(RoR2Content.Items.FocusConvergence, FocusedConvergences);
                    // Its body copies this when it spawns.
                    player.master.godMode = true;
                }
            };
        }

        /// <summary>
        /// Replaces a stage 1 pool: the run's first with Prime Meridian under Start at Prime Meridian, or any with
        /// the stage 5 maps under Start at stage 5, for <see cref="EclipseStagePool"/> to filter by their checkboxes
        /// and add Void Locus to like any stage 5 pool.
        /// </summary>
        internal static void ReplaceStageOne(Run run, WeightedSelection<SceneDef> choices)
        {
            if (!Applies(run) || choices.GetChoice(0).value.stageOrder != 1)
            {
                return;
            }

            SceneDef primeMeridian = SceneCatalog.FindSceneDef(PrimeMeridian);
            if (ModSettings.StartAtPrimeMeridian.Value && run.stageClearCount == 0)
            {
                // False when Seekers of the Storm is off for this run.
                if (primeMeridian && run.CanPickStage(primeMeridian))
                {
                    choices.Clear();
                    choices.AddChoice(primeMeridian, 1f);
                    Log.Info("Debug: Prime Meridian as the final stage instead of stage 1.");
                    return;
                }

                Log.Warning("Debug: Prime Meridian needs Seekers of the Storm, so the run starts as usual.");
            }

            if (!ModSettings.StartAtStageFive.Value)
            {
                return;
            }

            choices.Clear();
            foreach (SceneDef sceneDef in SceneCatalog.allStageSceneDefs)
            {
                if (sceneDef.stageOrder == 5 && run.CanPickStage(sceneDef))
                {
                    choices.AddChoice(sceneDef, 1f);
                }
            }

            Log.Info("Debug: a stage 5 instead of stage 1.");
        }

        private static bool Applies(Run run) =>
            (ModSettings.StartAtStageFive.Value || ModSettings.StartAtPrimeMeridian.Value) && EclipseStagePool.IsActive(run);
    }
}
