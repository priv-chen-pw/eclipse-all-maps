using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// Debug / Start at stage 5, for testing the final stages. Every stage 1 pick (the run's first stage, and each
    /// loop's after a final stage) draws from the whole stage 5 pool instead (Only added maps aside), the run counts
    /// stages 1-4 as cleared, and every player starts with 20 Mochas, 20 Hopoo Feathers, 10 Focused Convergences
    /// and god mode. Leaving the stage 5 begins a loop as usual, so the stage after it counts as the final stage.
    /// </summary>
    internal static class DebugStageFive
    {
        /// <summary>
        /// Stages cleared before stage 5. Without them the run would be at stage 1 difficulty, where the boss credits
        /// (600 × √difficulty) buy no stage 5 boss: those cost 800 and more.
        /// </summary>
        private const int StagesBeforeFive = 4;

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
                if (UnityEngine.Networking.NetworkServer.active && Applies(run) && run.stageClearCount == 0)
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
        /// Replaces a stage 1 pool with the stage 5 maps, for <see cref="EclipseStagePool"/> to filter by their
        /// checkboxes and add Void Locus to like any stage 5 pool.
        /// </summary>
        internal static void ReplaceStageOne(Run run, WeightedSelection<SceneDef> choices)
        {
            if (!Applies(run) || choices.GetChoice(0).value.stageOrder != 1)
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

        private static bool Applies(Run run) => ModSettings.StartAtStageFive.Value && EclipseStagePool.IsActive(run);
    }
}
